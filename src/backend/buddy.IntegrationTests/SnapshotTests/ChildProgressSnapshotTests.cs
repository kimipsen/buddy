using System.Text.Json.Nodes;

using Alba;

using buddy.Features.Calendars;
using buddy.Features.Progress;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Calendars;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.Progress;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Xunit;

namespace buddy.IntegrationTests.SnapshotTests;

// Verifies the inline Marten snapshot (ChildProgressSnapshotProjection, schema "snapshots") stays
// exactly consistent with a full replay-from-events rehydration after a sequence of commands --
// the invariant docs/backend/analysis/event-stream-snapshots.md relies on to say events remain
// the single source of truth and the snapshot is just a derived, quicker-to-read cache of the
// same state. Also exercises the CompletionTargetJsonConverter round-trip directly:
// ChildProgress.AwardedOccurrences is an ImmutableHashSet of OccurrenceKey, whose CompletionTarget
// union plain System.Text.Json can't serialize without that converter.
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

        // Direct assertion on the set round-trip: ChildProgress.AwardedOccurrences is an
        // ImmutableHashSet<OccurrenceKey>. A set element whose CompletionTarget didn't round-trip
        // could collapse the same way on both sides, which
        // Assert.Equivalent alone wouldn't distinguish from a correctly-populated set if both
        // sides happened to collapse the same way -- so assert directly that the snapshot's set is
        // non-empty and contains the expected surviving occurrence (the first task's star, which
        // was never revoked).
        Assert.NotEmpty(snapshot!.AwardedOccurrences);
        Assert.Contains(new OccurrenceKey(new CalendarItemId(firstTask!.Id), dueDate, new CompletionTarget.WholeTask()), snapshot.AwardedOccurrences);
        Assert.DoesNotContain(new OccurrenceKey(new CalendarItemId(secondTask!.Id), dueDate, new CompletionTarget.WholeTask()), snapshot.AwardedOccurrences);
    }

    // RewardsConfigured, RewardRequested and all three resolutions -- the reward catalog and the
    // request list (with its RewardRequestStatus enum and nullable ResolvedBy) must round-trip.
    [Fact]
    public async Task The_snapshot_matches_a_full_replay_after_reward_requests()
    {
        var family = await RewardTestHelpers.CreateFamilyAsync(fixture);
        await RewardTestHelpers.EarnStarsAsync(fixture, family, 4);
        var rewardId = await RewardTestHelpers.AddRewardAsync(fixture, family, "Screen time", 1);

        var approved = await RewardTestHelpers.RequestAsync(fixture, family, rewardId);
        var declined = await RewardTestHelpers.RequestAsync(fixture, family, rewardId);
        var cancelled = await RewardTestHelpers.RequestAsync(fixture, family, rewardId);
        await RewardTestHelpers.RequestAsync(fixture, family, rewardId);
        await RewardTestHelpers.ResolveAsync(fixture, family.GuardianToken, family.ChildId, approved, "approve");
        await RewardTestHelpers.ResolveAsync(fixture, family.GuardianToken, family.ChildId, declined, "decline");
        await RewardTestHelpers.CancelAsync(fixture, family.ChildToken, cancelled);

        var progress = fixture.Host.Services.GetRequiredService<IProgressEventStore>();
        var id = ProgressId.ForChild(new UserId(family.ChildId));

        var replayed = ChildProgress.Rehydrate(await progress.ReadAsync(id, CancellationToken.None));
        var snapshot = await progress.FindSnapshotAsync(id, CancellationToken.None);

        Assert.NotNull(replayed);
        Assert.NotNull(snapshot);
        Assert.Equivalent(replayed, snapshot, strict: true);
        Assert.Equal(1, snapshot.SpentStars);
        Assert.Equal(
            [RewardRequestStatus.Approved, RewardRequestStatus.Declined, RewardRequestStatus.Cancelled, RewardRequestStatus.Pending],
            snapshot.RewardRequests.Select(r => r.Status));
    }

    // FlashCardStarsAwarded with several sets and days: the populated array (and its pruning) must
    // round-trip through the real store and match a replay.
    [Fact]
    public async Task The_snapshot_matches_a_full_replay_with_a_populated_FlashCardStarsByDay()
    {
        var (progress, id, child) = await StartStreamAsync();
        var setA = Guid.CreateVersion7();
        var setB = Guid.CreateVersion7();
        var day = new DateOnly(2025, 6, 10);

        await progress.AppendAsync(
            id,
            [
                new FlashCardStarsAwarded(id, setA, day, 3, Guid.NewGuid(), DateTimeOffset.UtcNow),
                new FlashCardStarsAwarded(id, setB, day, 2, Guid.NewGuid(), DateTimeOffset.UtcNow),
                new FlashCardStarsAwarded(id, setA, day, 1, Guid.NewGuid(), DateTimeOffset.UtcNow),
                new FlashCardStarsAwarded(id, setA, day.AddDays(9), 4, Guid.NewGuid(), DateTimeOffset.UtcNow)
            ],
            CancellationToken.None);

        var replayed = ChildProgress.Rehydrate(await progress.ReadAsync(id, CancellationToken.None));
        var snapshot = await progress.FindSnapshotAsync(id, CancellationToken.None);

        Assert.NotNull(replayed);
        Assert.NotNull(snapshot);
        Assert.Equivalent(replayed, snapshot, strict: true);
        Assert.Equal(10, snapshot.TotalStars);
        // The two entries of day were pruned by the award 9 days later.
        Assert.Equal<FlashCardDayStars>([new FlashCardDayStars(setA, day.AddDays(9), 4)], snapshot.FlashCardStarsByDay.AsEnumerable());
        _ = child;
    }

    // A snapshot written before flash cards has no FlashCardStarsByDay: it must load as empty with the
    // rest of the state intact, and the next award folds onto it.
    [Fact]
    public async Task An_older_snapshot_without_FlashCardStarsByDay_loads_as_empty()
    {
        var (progress, id, _) = await StartStreamAsync();
        await progress.AppendAsync(id, [new FlashCardStarsAwarded(id, Guid.NewGuid(), new DateOnly(2025, 6, 10), 3, Guid.NewGuid(), DateTimeOffset.UtcNow)], CancellationToken.None);

        await RewriteSnapshotAsync(id, progress => progress.Remove(Member(progress, "FlashCardStarsByDay")));

        var snapshot = await progress.FindSnapshotAsync(id, CancellationToken.None);

        Assert.NotNull(snapshot);
        Assert.Empty(snapshot.FlashCardStarsByDay);
        Assert.Equal(3, snapshot.TotalStars);
    }

    // A snapshot carrying a member this build does not know (written by a newer build) still loads.
    [Fact]
    public async Task A_snapshot_with_an_unknown_extra_member_still_loads_with_its_flash_card_state()
    {
        var (progress, id, _) = await StartStreamAsync();
        var set = Guid.NewGuid();
        await progress.AppendAsync(id, [new FlashCardStarsAwarded(id, set, new DateOnly(2025, 6, 10), 3, Guid.NewGuid(), DateTimeOffset.UtcNow)], CancellationToken.None);

        await RewriteSnapshotAsync(id, progress => progress["SomethingFromTheFuture"] = new JsonObject { ["a"] = 1 });

        var snapshot = await progress.FindSnapshotAsync(id, CancellationToken.None);

        Assert.NotNull(snapshot);
        Assert.Equal(3, snapshot.TotalStars);
        Assert.Equal<FlashCardDayStars>([new FlashCardDayStars(set, new DateOnly(2025, 6, 10), 3)], snapshot.FlashCardStarsByDay.AsEnumerable());
    }

    private async Task<(IProgressEventStore Progress, ProgressId Id, UserId Child)> StartStreamAsync()
    {
        var progress = fixture.Host.Services.GetRequiredService<IProgressEventStore>();
        var child = new UserId(Guid.CreateVersion7());
        var id = ProgressId.ForChild(child);
        await progress.CreateAsync(id, [new ProgressStarted(id, child, DateTimeOffset.UtcNow)], CancellationToken.None);

        return (progress, id, child);
    }

    // Rewrites the ChildProgress member of the stored snapshot JSON in place, to stand in for a
    // snapshot written by another build. Member names are matched ignoring case (Marten's casing).
    private async Task RewriteSnapshotAsync(ProgressId id, Action<JsonObject> rewrite)
    {
        // Straight on the shared data source (autocommit), not through a Marten session's connection.
        var dataSource = fixture.Host.Services.GetRequiredService<NpgsqlDataSource>();

        await using var read = dataSource.CreateCommand("select data::text from snapshots.mt_doc_childprogresssnapshot where id = @id");
        read.Parameters.AddWithValue("id", id.Value);
        var json = (string?)await read.ExecuteScalarAsync();
        Assert.NotNull(json);

        var root = JsonNode.Parse(json)!.AsObject();
        rewrite(root[Member(root, "ChildProgress")]!.AsObject());

        await using var write = dataSource.CreateCommand("update snapshots.mt_doc_childprogresssnapshot set data = @data::jsonb where id = @id");
        write.Parameters.AddWithValue("id", id.Value);
        write.Parameters.AddWithValue("data", root.ToJsonString());

        Assert.Equal(1, await write.ExecuteNonQueryAsync());
    }

    private static string Member(JsonObject obj, string name) =>
        obj.Select(p => p.Key).Single(k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase));
}
