using System.Text.RegularExpressions;

using Alba;

using buddy.Features.Calendars;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.TaskLibrary;
using buddy.IntegrationTests.Fixtures;

using Xunit;

namespace buddy.IntegrationTests.Features.Calendars.GetIcalFeed;

// docs/backend/analysis/calendar-item-alarms.md, Questions 3 and 4: one VALARM per offset on each
// occurrence of an item with alarms, none on an item without, and only the first subtask of a
// routine's run.
[Collection(BuddyApiCollection.Name)]
public sealed class GetIcalFeedAlarmTests(BuddyApiFixture fixture)
{
    [Fact]
    public async Task An_item_with_two_alarms_gets_two_valarms_and_its_sibling_gets_none()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        await CalendarTestHelpers.CreateEventAsync(fixture, token, calendarId, "Dentist", alarms: [-120, -1440]);
        await CalendarTestHelpers.CreateEventAsync(fixture, token, calendarId, "Football");

        var ics = await ReadFeedAsync(token, calendarId);

        Assert.Equal(2, Regex.Count(ics, "BEGIN:VEVENT"));
        Assert.Equal(2, Regex.Count(ics, "BEGIN:VALARM"));
        Assert.Contains("TRIGGER:-PT2H", ics);
        Assert.Contains("TRIGGER:-P1D", ics);
        Assert.Equal(2, Regex.Count(ics, "ACTION:DISPLAY"));
        Assert.Equal(2, Regex.Count(ics, "DESCRIPTION:Dentist"));

        var football = Regex.Match(ics, "BEGIN:VEVENT(?:(?!END:VEVENT).)*SUMMARY:Football.*?END:VEVENT", RegexOptions.Singleline).Value;
        Assert.NotEmpty(football);
        Assert.DoesNotContain("VALARM", football);
    }

    [Fact]
    public async Task An_alarm_at_the_start_and_one_days_and_hours_before_are_written_in_nominal_units()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        await CalendarTestHelpers.CreateTaskAsync(fixture, token, calendarId, "Trip", isAllDay: true, alarms: [0, -2340]);

        var ics = await ReadFeedAsync(token, calendarId);

        Assert.Equal(1, Regex.Count(ics, "TRIGGER:P0D"));
        Assert.Equal(1, Regex.Count(ics, "TRIGGER:-P1DT15H"));
    }

    [Fact]
    public async Task Every_occurrence_of_a_repeating_task_carries_its_alarm()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        await CalendarTestHelpers.CreateTaskAsync(
            fixture, token, calendarId, "Take out trash", start,
            new RecurrenceRuleRequest(RecurrenceFrequency.Daily, 1, start.AddDays(2)), alarms: [-15]);

        var ics = await ReadFeedAsync(token, calendarId);

        Assert.Equal(3, Regex.Count(ics, "BEGIN:VTODO"));
        Assert.Equal(3, Regex.Count(ics, "TRIGGER:-PT15M"));
    }

    [Fact]
    public async Task An_all_day_alarm_on_the_day_at_nine_is_written_as_a_positive_trigger()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        await CalendarTestHelpers.CreateEventAsync(fixture, token, calendarId, "Birthday", isAllDay: true, alarms: [540, -360]);

        var ics = await ReadFeedAsync(token, calendarId);

        Assert.Contains("TRIGGER:PT9H", ics);
        Assert.Contains("TRIGGER:-PT6H", ics);
    }

    [Fact]
    public async Task A_routine_carries_its_alarm_on_the_first_subtask_of_each_run_only()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, guardianToken, "Family");
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, guardianToken, "Family", groupId);
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Url($"/groups/{groupId}/children/{child.Id}");
            _.StatusCodeShouldBe(204);
        });

        var template = await TaskLibraryTestHelpers.CreateTaskTemplateAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(template);
        await TaskLibraryTestHelpers.AddSubtaskAsync(fixture, guardianToken, template.Id, new AddSubtaskOptions("Brush teeth", "toothbrush", "00:10:00"));
        await TaskLibraryTestHelpers.AddSubtaskAsync(fixture, guardianToken, template.Id, new AddSubtaskOptions("Get dressed", "shirt", "00:15:00"));

        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var item = await CalendarTestHelpers.ScheduleTaskFromTemplateAsync(
            fixture, guardianToken, calendarId, template.Id, "Morning routine", start, new TimeOnly(7, 0),
            new RecurrenceRuleRequest(RecurrenceFrequency.Daily, 1, start.AddDays(1)), child.Id, alarms: [-5]);
        Assert.NotNull(item);
        Assert.Equal([-5], item.Alarms);

        var ics = await ReadFeedAsync(guardianToken, calendarId);

        Assert.Equal(4, Regex.Count(ics, "BEGIN:VTODO"));
        Assert.Equal(2, Regex.Count(ics, "TRIGGER:-PT5M"));
        Assert.Equal(2, Regex.Count(ics, "DESCRIPTION:Brush teeth"));

        var occurrences = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/calendars/{calendarId}/occurrences?from={start:yyyy-MM-dd}&to={start.AddDays(1):yyyy-MM-dd}");
            _.StatusCodeShouldBeOk();
        });

        var subtasks = occurrences.ReadAsJson<List<CalendarItemOccurrenceDto>>();
        Assert.Equal(
            [("Brush teeth", 1), ("Get dressed", 0), ("Brush teeth", 1), ("Get dressed", 0)],
            subtasks.Select(o => (o.Title, o.Alarms!.Count)));
    }

    private async Task<string> ReadFeedAsync(string token, Guid calendarId)
    {
        var issued = await CalendarTestHelpers.CreateIcalTokenAsync(fixture, token, calendarId);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.Get.Url($"/calendars/{calendarId}/ical/{issued.Token}");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsText();
    }
}
