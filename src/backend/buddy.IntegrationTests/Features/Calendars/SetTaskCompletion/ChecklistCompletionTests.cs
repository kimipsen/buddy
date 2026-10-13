using Alba;

using buddy.Features.Calendars;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Features.Progress;
using buddy.IntegrationTests.Features.TaskLibrary;
using buddy.IntegrationTests.Fixtures;

using Xunit;

using static buddy.IntegrationTests.Features.TaskLibrary.ChecklistTestHelpers;

namespace buddy.IntegrationTests.Features.Calendars.SetTaskCompletion;

// Ticking a packing list: tomorrow's list may be packed tonight, an item only counts on its own
// weekdays, and the whole list earns one star (docs/backend/analysis/packing-checklists.md,
// Questions 3-5). Dates are the calendar's own "today" (Europe/Copenhagen).
[Collection(BuddyApiCollection.Name)]
public sealed class ChecklistCompletionTests(BuddyApiFixture fixture)
{
    private sealed record Scheduled(ChecklistFamily Family, Guid CalendarId, Guid TemplateId, Guid ItemId);

    // A daily packing list for the child, scheduled from today on a family calendar the child is in.
    private async Task<Guid> FamilyCalendarAsync(ChecklistFamily family)
    {
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, family.GuardianToken, "Family");
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, family.GuardianToken, "Family", groupId);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {family.GuardianToken}");
            _.Put.Url($"/groups/{groupId}/children/{family.Child.Id}");
            _.StatusCodeShouldBe(204);
        });

        return calendarId;
    }

    private async Task<Scheduled> ScheduleChecklistAsync(params (string Title, string[]? Weekdays)[] items)
    {
        var family = await CreateFamilyAsync(fixture);
        var calendarId = await FamilyCalendarAsync(family);

        var checklist = await CreateChecklistAsync(fixture, family.GuardianToken, family.Child.Id, eveningBeforeFrom: "17:00:00");

        foreach (var (title, weekdays) in items)
        {
            await AddItemAsync(fixture, family.GuardianToken, checklist.Id, title, weekdays);
        }

        var item = await CalendarTestHelpers.ScheduleTaskFromTemplateAsync(
            fixture, family.GuardianToken, calendarId, checklist.Id, "School bag", CalendarToday(), new TimeOnly(7, 45),
            new RecurrenceRuleRequest(RecurrenceFrequency.Daily, 1, null), assignedTo: family.Child.Id);

        return new Scheduled(family, calendarId, checklist.Id, item!.Id);
    }

    private async Task<CalendarItemOccurrenceDto[]> OccurrencesAsync(Scheduled scheduled, DateOnly date)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {scheduled.Family.ChildToken}");
            _.Get.Url($"/calendars/{scheduled.CalendarId}/occurrences?from={date:yyyy-MM-dd}&to={date:yyyy-MM-dd}");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<CalendarItemOccurrenceDto[]>();
    }

    private Task<IScenarioResult> TickAsync(Scheduled scheduled, Guid subtaskId, DateOnly date, bool isCompleted = true, int expectedStatus = 200) =>
        fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {scheduled.Family.ChildToken}");
            _.Patch.Json(new { Date = date, IsCompleted = isCompleted })
                .ToUrl($"/calendars/{scheduled.CalendarId}/items/{scheduled.ItemId}/subtasks/{subtaskId}/completion");
            _.StatusCodeShouldBe(expectedStatus);
        });

    private async Task<int> StarsAsync(Scheduled scheduled) =>
        (await RewardTestHelpers.GetMyProgressAsync(fixture, scheduled.Family.ChildToken)).TotalStars;

    [Fact]
    public async Task A_checklists_occurrences_are_due_at_the_leave_by_time_and_carry_its_options()
    {
        var scheduled = await ScheduleChecklistAsync(("Lunchbox", null), ("Glasses", null));

        var occurrences = await OccurrencesAsync(scheduled, CalendarToday());

        Assert.Equal(2, occurrences.Length);
        Assert.All(occurrences, o =>
        {
            Assert.Equal(1, o.Timing.Kind);
            Assert.Equal(new TimeOnly(17, 0), o.Routine?.Checklist?.EveningBeforeFrom);
        });
    }

    [Fact]
    public async Task Tomorrows_list_can_be_packed_tonight_but_not_the_day_after()
    {
        var scheduled = await ScheduleChecklistAsync(("Lunchbox", null));
        var tomorrow = CalendarToday().AddDays(1);
        var itemId = Assert.Single(await OccurrencesAsync(scheduled, tomorrow)).SubtaskId!.Value;

        await TickAsync(scheduled, itemId, tomorrow);
        await TickAsync(scheduled, itemId, tomorrow.AddDays(1), expectedStatus: 400);

        Assert.True(Assert.Single(await OccurrencesAsync(scheduled, tomorrow)).IsCompleted);
        Assert.False(Assert.Single(await OccurrencesAsync(scheduled, CalendarToday())).IsCompleted);
    }

    [Fact]
    public async Task A_routine_step_still_cannot_be_ticked_for_tomorrow()
    {
        var family = await CreateFamilyAsync(fixture);
        var calendarId = await FamilyCalendarAsync(family);
        var routine = await TaskLibraryTestHelpers.CreateTaskTemplateAsync(fixture, family.GuardianToken, family.Child.Id);
        var withStep = await TaskLibraryTestHelpers.AddSubtaskAsync(fixture, family.GuardianToken, routine!.Id);
        var tomorrow = CalendarToday().AddDays(1);
        var item = await CalendarTestHelpers.ScheduleTaskFromTemplateAsync(fixture, family.GuardianToken, calendarId, routine.Id, startDate: tomorrow, assignedTo: family.Child.Id);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {family.GuardianToken}");
            _.Patch.Json(new { Date = tomorrow, IsCompleted = true })
                .ToUrl($"/calendars/{calendarId}/items/{item!.Id}/subtasks/{withStep!.Subtasks[0].Id}/completion");
            _.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task An_item_cannot_be_ticked_on_a_day_it_does_not_apply_but_can_be_unticked()
    {
        var today = CalendarToday();
        var otherDays = Enum.GetValues<DayOfWeek>().Where(d => d != today.DayOfWeek).Select(d => d.ToString()).ToArray();
        var scheduled = await ScheduleChecklistAsync(("Lunchbox", null), ("Swimming kit", otherDays));
        var template = Assert.Single(await ListAsync(fixture, scheduled.Family.GuardianToken, scheduled.Family.Child.Id));
        var swimmingKit = template.Subtasks.Single(s => s.Title == "Swimming kit").Id;

        Assert.Equal(["Lunchbox"], (await OccurrencesAsync(scheduled, today)).Select(o => o.Title));

        await TickAsync(scheduled, swimmingKit, today, expectedStatus: 400);
        await TickAsync(scheduled, swimmingKit, today, isCompleted: false);
    }

    [Fact]
    public async Task The_last_item_ticked_earns_one_star_and_unticking_any_item_takes_it_back()
    {
        var scheduled = await ScheduleChecklistAsync(("Lunchbox", null), ("Glasses", null), ("Homework folder", null));
        var today = CalendarToday();
        var ids = (await OccurrencesAsync(scheduled, today)).Select(o => o.SubtaskId!.Value).ToArray();

        await TickAsync(scheduled, ids[0], today);
        await TickAsync(scheduled, ids[1], today);
        Assert.Equal(0, await StarsAsync(scheduled));

        await TickAsync(scheduled, ids[2], today);
        Assert.Equal(1, await StarsAsync(scheduled));

        // Ticking an already ticked item again changes nothing.
        await TickAsync(scheduled, ids[2], today);
        Assert.Equal(1, await StarsAsync(scheduled));

        await TickAsync(scheduled, ids[0], today, isCompleted: false);
        Assert.Equal(0, await StarsAsync(scheduled));

        await TickAsync(scheduled, ids[0], today);
        Assert.Equal(1, await StarsAsync(scheduled));
    }

    [Fact]
    public async Task An_item_approved_after_the_list_was_packed_keeps_the_star_and_counts_from_the_next_day()
    {
        var scheduled = await ScheduleChecklistAsync(("Lunchbox", null));
        var family = scheduled.Family;
        var today = CalendarToday();
        var tomorrow = today.AddDays(1);
        var lunchbox = Assert.Single(await OccurrencesAsync(scheduled, today)).SubtaskId!.Value;

        await TickAsync(scheduled, lunchbox, today);
        Assert.Equal(1, await StarsAsync(scheduled));

        var suggestionId = await SuggestIdAsync(fixture, family.ChildToken, scheduled.TemplateId, "Recorder");
        var approved = await ApproveAsync(fixture, family.GuardianToken, scheduled.TemplateId, suggestionId, "Recorder");
        var recorder = approved.ReadAsJson<TaskTemplateDto>().Subtasks.Single(s => s.Title == "Recorder").Id;

        // Today's star stays; ticking the new item earns nothing more.
        Assert.Equal(1, await StarsAsync(scheduled));
        await TickAsync(scheduled, recorder, today);
        Assert.Equal(1, await StarsAsync(scheduled));

        // Tomorrow's list needs the new item too.
        await TickAsync(scheduled, lunchbox, tomorrow);
        Assert.Equal(1, await StarsAsync(scheduled));
        await TickAsync(scheduled, recorder, tomorrow);
        Assert.Equal(2, await StarsAsync(scheduled));
    }
}
