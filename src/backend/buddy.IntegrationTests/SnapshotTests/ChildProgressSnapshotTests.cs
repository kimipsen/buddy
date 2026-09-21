using Alba;

using buddy.Features.Calendars;
using buddy.Features.Progress;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Calendars;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.SnapshotTests;

// Verifies the inline Marten snapshot (ChildProgressSnapshotProjection, schema "snapshots") stays
// exactly consistent with a full replay-from-events rehydration after a sequence of commands --
// the invariant docs/backend/analysis/event-stream-snapshots.md relies on to say events remain
// the single source of truth and the snapshot is just a derived, quicker-to-read cache of the
// same state. Also exercises the ValueTupleJsonConverterFactory round-trip directly:
// ChildProgress.AwardedOccurrences is an ImmutableHashSet of 3-element tuples, which plain
// System.Text.Json would silently serialize as empty objects without that converter.
[Collection(BuddyApiCollection.Name)]
public sealed class ChildProgressSnapshotTests(BuddyApiFixture fixture)
{
    [Fact]
    public async Task The_snapshot_matches_a_full_replay_after_several_commands()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, guardianToken, "Snapshot Check");
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, guardianToken, "Family", groupId);
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Url($"/groups/{groupId}/children/{child.Id}");
            _.StatusCodeShouldBe(204);
        });

        // GoalPostsConfigured, first event of the stream (ConfigureGoalPosts creates the stream
        // when it doesn't exist yet -- see ConfigureGoalPostsHandler).
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { GoalPosts = new[] { new { Threshold = 1, Icon = "🥉", Label = (string?)null } } })
                .ToUrl($"/progress/children/{child.Id}/goals");
            _.StatusCodeShouldBeOk();
        });

        var dueDate = DateOnly.FromDateTime(DateTime.UtcNow);

        // StarAwarded + MilestoneUnlocked (crosses the threshold=1 goal post just configured).
        var firstTask = await CalendarTestHelpers.CreateTaskAsync(fixture, guardianToken, calendarId, dueDate: dueDate, assignedTo: child.Id);
        Assert.NotNull(firstTask);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Patch.Json(new { Date = dueDate, IsCompleted = true })
                .ToUrl($"/calendars/{calendarId}/items/{firstTask.Id}/completion");
            _.StatusCodeShouldBeOk();
        });

        // A second StarAwarded on a distinct item.
        var secondTask = await CalendarTestHelpers.CreateTaskAsync(fixture, guardianToken, calendarId, dueDate: dueDate, assignedTo: child.Id);
        Assert.NotNull(secondTask);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Patch.Json(new { Date = dueDate, IsCompleted = true })
                .ToUrl($"/calendars/{calendarId}/items/{secondTask.Id}/completion");
            _.StatusCodeShouldBeOk();
        });

        // StarRevoked for the second item, exercising AwardedOccurrences.Remove too.
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Patch.Json(new { Date = dueDate, IsCompleted = false })
                .ToUrl($"/calendars/{calendarId}/items/{secondTask.Id}/completion");
            _.StatusCodeShouldBeOk();
        });

        var progress = fixture.Host.Services.GetRequiredService<IProgressEventStore>();
        var id = ProgressId.ForChild(new UserId(child.Id));

        var events = await progress.ReadAsync(id, CancellationToken.None);
        var replayed = ChildProgress.Rehydrate(events);
        var snapshot = await progress.FindSnapshotAsync(id, CancellationToken.None);

        Assert.NotNull(replayed);
        Assert.NotNull(snapshot);
        Assert.Equivalent(replayed, snapshot, strict: true);

        // Direct assertion on the tuple-set round-trip: ChildProgress.AwardedOccurrences is an
        // ImmutableHashSet<(CalendarItemId, DateOnly, Guid?)>. Plain System.Text.Json silently
        // serializes a tuple set element as "{}" without ValueTupleJsonConverterFactory, which
        // Assert.Equivalent alone wouldn't distinguish from a correctly-populated set if both
        // sides happened to collapse the same way -- so assert directly that the snapshot's set is
        // non-empty and contains the expected surviving occurrence (the first task's star, which
        // was never revoked).
        Assert.NotEmpty(snapshot!.AwardedOccurrences);
        Assert.Contains((new CalendarItemId(firstTask!.Id), dueDate, (Guid?)null), snapshot.AwardedOccurrences);
        Assert.DoesNotContain((new CalendarItemId(secondTask!.Id), dueDate, (Guid?)null), snapshot.AwardedOccurrences);
    }
}
