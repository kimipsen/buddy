using Alba;

using buddy.Features.Calendars;
using buddy.IntegrationTests.Features.Calendars;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.SnapshotTests;

// Verifies the inline Marten snapshot (CalendarItemSnapshotProjection, schema "snapshots") stays
// exactly consistent with a full replay-from-events rehydration after a sequence of commands --
// same invariant as CalendarSnapshotTests/GroupSnapshotTests. Deliberately includes a
// TaskCompletionChanged step: CalendarItem.CompletionLog is a set of CompletionKey, whose
// CompletionTarget union only round-trips through CompletionTargetJsonConverter -- this is the one
// command sequence that exercises that converter through the snapshot projection's storage.
[Collection(BuddyApiCollection.Name)]
public sealed class CalendarItemSnapshotTests(BuddyApiFixture fixture)
{
    [Fact]
    public async Task The_snapshot_matches_a_full_replay_after_several_commands()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, ownerToken, "Snapshot Check");
        // Today, not a future date -- SetTaskCompletionHandler rejects marking a future occurrence
        // complete ("Cannot mark a future occurrence as complete."), so the completion step below
        // needs an occurrence date that's already due.
        var dueDate = DateOnly.FromDateTime(DateTime.UtcNow);

        var created = await CalendarTestHelpers.CreateTaskAsync(fixture, ownerToken, calendarId, "File taxes", dueDate);
        var itemId = created!.Id;

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Patch.Json(new { Date = dueDate, IsCompleted = true }).ToUrl($"/calendars/{calendarId}/items/{itemId}/completion");
            _.StatusCodeShouldBeOk();
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Patch.Json(new { Title = "File taxes (amended)", Icon = "task", Color = "#123456" }).ToUrl($"/calendars/{calendarId}/items/{itemId}/details");
            _.StatusCodeShouldBeOk();
        });

        var items = fixture.Host.Services.GetRequiredService<ICalendarItemEventStore>();
        var id = new CalendarItemId(itemId);

        var events = await items.ReadAsync(id, CancellationToken.None);
        var replayed = CalendarItem.Rehydrate(events);
        var snapshot = await items.FindSnapshotAsync(id, CancellationToken.None);

        Assert.NotNull(replayed);
        Assert.NotNull(snapshot);
        Assert.Equivalent(replayed, snapshot, strict: true);
    }

    // ItemScheduleJsonConverter round-trips each case: an event's Period, and a task's assignee.
    [Fact]
    public async Task The_snapshot_matches_a_full_replay_for_an_event_and_an_assigned_task()
    {
        var (_, ownerToken, ownerId) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, ownerToken, "Snapshot Cases");
        var day = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2);

        var @event = await CalendarTestHelpers.CreateEventAsync(fixture, ownerToken, calendarId, isAllDay: true);
        var task = await CalendarTestHelpers.CreateTaskAsync(fixture, ownerToken, calendarId, "Walk the dog", day, assignedTo: ownerId);

        await AssertSnapshotMatchesReplayAsync(@event!.Id);
        await AssertSnapshotMatchesReplayAsync(task!.Id);
    }

    // RecurrenceJsonConverter round-trips a repeating recurrence with an end date.
    [Fact]
    public async Task The_snapshot_matches_a_full_replay_for_a_repeating_item()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, ownerToken, "Snapshot Recurrence");
        var day = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var task = await CalendarTestHelpers.CreateTaskAsync(fixture, ownerToken, calendarId, "Water plants", day);
        Assert.NotNull(task);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Patch.Json(new { Recurrence = new { Frequency = RecurrenceFrequency.Monthly, IntervalCount = 2, Until = (DateOnly?)day.AddDays(90) } })
                .ToUrl($"/calendars/{calendarId}/items/{task.Id}/recurrence");
            _.StatusCodeShouldBeOk();
        });

        await AssertSnapshotMatchesReplayAsync(task.Id);

        var snapshot = await fixture.Host.Services.GetRequiredService<ICalendarItemEventStore>()
            .FindSnapshotAsync(new CalendarItemId(task.Id), CancellationToken.None);
        Assert.Equal(
            new Recurrence.Repeating(RecurrenceFrequency.Monthly, 2, new RecurrenceEnd.On(day.AddDays(90))),
            snapshot!.Recurrence);
    }

    private async Task AssertSnapshotMatchesReplayAsync(Guid itemId)
    {
        var items = fixture.Host.Services.GetRequiredService<ICalendarItemEventStore>();
        var id = new CalendarItemId(itemId);

        var replayed = CalendarItem.Rehydrate(await items.ReadAsync(id, CancellationToken.None));
        var snapshot = await items.FindSnapshotAsync(id, CancellationToken.None);

        Assert.NotNull(replayed);
        Assert.NotNull(snapshot);
        Assert.Equivalent(replayed, snapshot, strict: true);
    }
}
