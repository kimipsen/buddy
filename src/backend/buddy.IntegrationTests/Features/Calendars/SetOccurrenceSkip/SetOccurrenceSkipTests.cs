using Alba;

using buddy.Features.Calendars;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.Features.Calendars.SetOccurrenceSkip;

// docs/backend/analysis/skip-occurrences.md -- skipping and restoring one occurrence of a
// repeating item, and how a skip shows up in the occurrence listing.
[Collection(BuddyApiCollection.Name)]
public sealed class SetOccurrenceSkipTests(BuddyApiFixture fixture)
{
    private static readonly RecurrenceRuleRequest Daily = new(RecurrenceFrequency.Daily, 1);
    private static readonly RecurrenceRuleRequest Weekly = new(RecurrenceFrequency.Weekly, 1);

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private async Task<List<CalendarItemOccurrenceDto>> OccurrencesAsync(string token, Guid calendarId, DateOnly from, DateOnly to, bool includeSkipped = false)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"/calendars/{calendarId}/occurrences?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}" + (includeSkipped ? "&includeSkipped=true" : ""));
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<List<CalendarItemOccurrenceDto>>();
    }

    private static DateOnly DateOf(CalendarItemOccurrenceDto occurrence) =>
        DateOnly.FromDateTime((occurrence.StartsAt ?? occurrence.DueAt)!.Value.Date);

    [Fact]
    [CoversEndpoint("SetOccurrenceSkip")]
    public async Task A_contributor_can_skip_one_occurrence_of_a_repeating_task_and_it_leaves_the_listing()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var start = Today.AddDays(1);
        var task = await CalendarTestHelpers.CreateTaskAsync(fixture, token, calendarId, "Football", start, Weekly);

        var skipped = await CalendarTestHelpers.SetOccurrenceSkipAsync(fixture, token, calendarId, task!.Id, start.AddDays(7), isSkipped: true);

        Assert.Equal(new OccurrenceSkipResponseDto(task.Id, start.AddDays(7), true), skipped);

        var occurrences = await OccurrencesAsync(token, calendarId, start, start.AddDays(14));
        Assert.Equal([start, start.AddDays(14)], occurrences.Select(DateOf));
        Assert.All(occurrences, o => Assert.False(o.IsSkipped));
        Assert.All(occurrences, o => Assert.True(o.IsRepeating));
    }

    [Fact]
    public async Task Including_skipped_occurrences_returns_the_skipped_day_flagged()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var start = Today.AddDays(1);
        var task = await CalendarTestHelpers.CreateTaskAsync(fixture, token, calendarId, "Football", start, Weekly);

        await CalendarTestHelpers.SetOccurrenceSkipAsync(fixture, token, calendarId, task!.Id, start.AddDays(7), isSkipped: true);

        var occurrences = await OccurrencesAsync(token, calendarId, start, start.AddDays(14), includeSkipped: true);
        Assert.Equal([start, start.AddDays(7), start.AddDays(14)], occurrences.Select(DateOf));
        Assert.Equal([false, true, false], occurrences.Select(o => o.IsSkipped));
    }

    [Fact]
    public async Task An_event_occurrence_can_be_skipped_too()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var start = Today.AddDays(1);
        var item = await CalendarTestHelpers.CreateEventAsync(fixture, token, calendarId, "Swimming", start);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Recurrence = Daily }).ToUrl($"/calendars/{calendarId}/items/{item!.Id}/recurrence");
            _.StatusCodeShouldBeOk();
        });

        await CalendarTestHelpers.SetOccurrenceSkipAsync(fixture, token, calendarId, item!.Id, start, isSkipped: true);

        var occurrences = await OccurrencesAsync(token, calendarId, start, start.AddDays(1));
        Assert.Equal([start.AddDays(1)], occurrences.Select(DateOf));
    }

    [Fact]
    public async Task Restoring_a_skipped_occurrence_brings_it_back_with_its_completion()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var task = await CalendarTestHelpers.CreateTaskAsync(fixture, token, calendarId, "Homework", Today, Daily);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Date = Today, IsCompleted = true }).ToUrl($"/calendars/{calendarId}/items/{task!.Id}/completion");
            _.StatusCodeShouldBeOk();
        });

        await CalendarTestHelpers.SetOccurrenceSkipAsync(fixture, token, calendarId, task!.Id, Today, isSkipped: true);
        Assert.Empty(await OccurrencesAsync(token, calendarId, Today, Today));

        var restored = await CalendarTestHelpers.SetOccurrenceSkipAsync(fixture, token, calendarId, task.Id, Today, isSkipped: false);

        Assert.False(restored!.IsSkipped);
        var occurrence = Assert.Single(await OccurrencesAsync(token, calendarId, Today, Today));
        Assert.True(occurrence.IsCompleted);
        Assert.False(occurrence.IsSkipped);
    }

    [Fact]
    public async Task Skipping_an_already_skipped_day_or_restoring_one_that_is_not_appends_nothing()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var start = Today.AddDays(1);
        var task = await CalendarTestHelpers.CreateTaskAsync(fixture, token, calendarId, "Homework", start, Daily);
        var store = fixture.Host.Services.GetRequiredService<ICalendarItemEventStore>();

        await CalendarTestHelpers.SetOccurrenceSkipAsync(fixture, token, calendarId, task!.Id, start, isSkipped: true);
        var eventsAfterSkip = (await store.ReadAsync(new CalendarItemId(task.Id), CancellationToken.None)).Count;

        var again = await CalendarTestHelpers.SetOccurrenceSkipAsync(fixture, token, calendarId, task.Id, start, isSkipped: true);
        await CalendarTestHelpers.SetOccurrenceSkipAsync(fixture, token, calendarId, task.Id, start.AddDays(1), isSkipped: false);

        Assert.True(again!.IsSkipped);
        Assert.Equal(eventsAfterSkip, (await store.ReadAsync(new CalendarItemId(task.Id), CancellationToken.None)).Count);
    }

    [Fact]
    public async Task A_one_off_item_cannot_skip_an_occurrence()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var task = await CalendarTestHelpers.CreateTaskAsync(fixture, token, calendarId, "File taxes", Today.AddDays(1));

        await CalendarTestHelpers.SetOccurrenceSkipAsync(fixture, token, calendarId, task!.Id, Today.AddDays(1), isSkipped: true, expectedStatus: 400);

        Assert.False(Assert.Single(await OccurrencesAsync(token, calendarId, Today.AddDays(1), Today.AddDays(1))).IsRepeating);
    }

    [Fact]
    public async Task A_day_the_rule_does_not_produce_cannot_be_skipped_but_can_be_restored()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var start = Today.AddDays(1);
        var task = await CalendarTestHelpers.CreateTaskAsync(fixture, token, calendarId, "Football", start, Weekly);

        await CalendarTestHelpers.SetOccurrenceSkipAsync(fixture, token, calendarId, task!.Id, start.AddDays(1), isSkipped: true, expectedStatus: 400);
        await CalendarTestHelpers.SetOccurrenceSkipAsync(fixture, token, calendarId, task.Id, start.AddDays(-7), isSkipped: true, expectedStatus: 400);

        var restored = await CalendarTestHelpers.SetOccurrenceSkipAsync(fixture, token, calendarId, task.Id, start.AddDays(1), isSkipped: false);
        Assert.False(restored!.IsSkipped);
    }

    [Fact]
    public async Task A_weekly_rule_on_chosen_weekdays_skips_only_those_weekdays()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var start = Today.AddDays(1);
        var otherDay = start.AddDays(3);
        var task = await CalendarTestHelpers.CreateTaskAsync(
            fixture, token, calendarId, "Swimming", start,
            new RecurrenceRuleRequest(RecurrenceFrequency.Weekly, 1, Weekdays: [start.DayOfWeek, otherDay.DayOfWeek]));

        await CalendarTestHelpers.SetOccurrenceSkipAsync(fixture, token, calendarId, task!.Id, otherDay, isSkipped: true);
        await CalendarTestHelpers.SetOccurrenceSkipAsync(fixture, token, calendarId, task.Id, start.AddDays(1), isSkipped: true, expectedStatus: 400);
    }

    // Found in review: a skip must stay restorable after the item stops repeating, or its only
    // occurrence would be hidden for good.
    [Fact]
    public async Task A_skip_can_be_restored_after_the_item_stops_repeating()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var start = Today.AddDays(1);
        var task = await CalendarTestHelpers.CreateTaskAsync(fixture, token, calendarId, "Football", start, Weekly);

        await CalendarTestHelpers.SetOccurrenceSkipAsync(fixture, token, calendarId, task!.Id, start, isSkipped: true);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Recurrence = (RecurrenceRuleRequest?)null }).ToUrl($"/calendars/{calendarId}/items/{task.Id}/recurrence");
            _.StatusCodeShouldBeOk();
        });
        Assert.Empty(await OccurrencesAsync(token, calendarId, start, start));

        await CalendarTestHelpers.SetOccurrenceSkipAsync(fixture, token, calendarId, task.Id, start, isSkipped: false);

        Assert.Equal([start], (await OccurrencesAsync(token, calendarId, start, start)).Select(DateOf));
    }

    [Fact]
    public async Task A_past_occurrence_can_be_skipped()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var start = Today.AddDays(-3);
        var task = await CalendarTestHelpers.CreateTaskAsync(fixture, token, calendarId, "Homework", start, Daily);

        await CalendarTestHelpers.SetOccurrenceSkipAsync(fixture, token, calendarId, task!.Id, start.AddDays(1), isSkipped: true);

        Assert.DoesNotContain(start.AddDays(1), (await OccurrencesAsync(token, calendarId, start, Today)).Select(DateOf));
    }

    [Fact]
    public async Task A_skipped_occurrence_cannot_be_marked_complete_but_can_be_unmarked()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var task = await CalendarTestHelpers.CreateTaskAsync(fixture, token, calendarId, "Homework", Today, Daily);

        await CalendarTestHelpers.SetOccurrenceSkipAsync(fixture, token, calendarId, task!.Id, Today, isSkipped: true);

        foreach (var (isCompleted, status) in new[] { (true, 400), (false, 200) })
        {
            await fixture.Host.Scenario(_ =>
            {
                _.WithRequestHeader("Authorization", $"Bearer {token}");
                _.Patch.Json(new { Date = Today, IsCompleted = isCompleted }).ToUrl($"/calendars/{calendarId}/items/{task.Id}/completion");
                _.StatusCodeShouldBe(status);
            });
        }
    }

    [Fact]
    public async Task A_child_with_viewer_access_cannot_skip_even_their_own_task()
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

        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var task = await CalendarTestHelpers.CreateTaskAsync(fixture, guardianToken, calendarId, "Tidy room", Today, Daily, assignedTo: child.Id);

        await CalendarTestHelpers.SetOccurrenceSkipAsync(fixture, childToken, calendarId, task!.Id, Today, isSkipped: true, expectedStatus: 403);
    }

    [Fact]
    public async Task A_stranger_gets_not_found()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, ownerToken, "Personal");
        var task = await CalendarTestHelpers.CreateTaskAsync(fixture, ownerToken, calendarId, "Homework", Today, Daily);

        await CalendarTestHelpers.SetOccurrenceSkipAsync(fixture, strangerToken, calendarId, task!.Id, Today, isSkipped: true, expectedStatus: 404);
    }

    [Fact]
    public async Task An_unknown_deleted_or_other_calendars_item_gets_not_found()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var otherCalendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Other");
        var deleted = await CalendarTestHelpers.CreateTaskAsync(fixture, token, calendarId, "Gone", Today, Daily);
        var elsewhere = await CalendarTestHelpers.CreateTaskAsync(fixture, token, otherCalendarId, "Elsewhere", Today, Daily);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url($"/calendars/{calendarId}/items/{deleted!.Id}");
            _.StatusCodeShouldBe(204);
        });

        await CalendarTestHelpers.SetOccurrenceSkipAsync(fixture, token, calendarId, Guid.CreateVersion7(), Today, isSkipped: true, expectedStatus: 404);
        await CalendarTestHelpers.SetOccurrenceSkipAsync(fixture, token, calendarId, deleted!.Id, Today, isSkipped: true, expectedStatus: 404);
        await CalendarTestHelpers.SetOccurrenceSkipAsync(fixture, token, calendarId, elsewhere!.Id, Today, isSkipped: true, expectedStatus: 404);
    }
}
