using buddy.Features.FlashCards;
using buddy.Features.Progress;
using buddy.Features.Users;
using buddy.IntegrationTests.Common.Concurrency;
using buddy.IntegrationTests.Fixtures;

using JasperFx;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.FlashCards.FlashCardTestHelpers;

namespace buddy.IntegrationTests.Features.FlashCards.FinishFlashCardGame;

// Finish writes the progress stream and then the practice stream, one saved session each. These
// tests break the second write (or race the first) through the decorators and assert the progress
// stream directly, so a later move of finish into one shared unit of work cannot silently turn the
// recovery tests into passing no-ops.
[Collection(BuddyApiCollection.Name)]
public sealed class FinishFlashCardGameRecoveryTests(BuddyApiFixture fixture) : IAsyncLifetime
{
    private readonly List<Guid> _children = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        foreach (var child in _children)
        {
            TestFlashCardPracticeEventStore.Disarm(child);
            ConcurrentWriterProgressEventStore.Disarm(child);
        }

        return Task.CompletedTask;
    }

    private IFlashCardPracticeEventStore Practices => fixture.Host.Services.GetRequiredService<IFlashCardPracticeEventStore>();

    private TestFlashCardPracticeEventStore PracticeDecorator => fixture.Host.Services.GetRequiredService<TestFlashCardPracticeEventStore>();

    private IProgressEventStore Progress => fixture.Host.Services.GetRequiredService<IProgressEventStore>();

    private async Task<(string GuardianToken, Guid ChildId, string ChildToken, FlashCardSetResponse Set, EnabledFlashCardSetResponse Enabled)> SetUpAsync()
    {
        var (guardianToken, _, child, childToken) = await CreateFamilyAsync(fixture);
        var set = await CreatePlayableSetAsync(fixture, guardianToken);
        var enabled = await EnableAsync(fixture, guardianToken, child.Id, set.Id);
        _children.Add(child.Id);

        return (guardianToken, child.Id, childToken, set, enabled);
    }

    private static FlashCardPracticeId PracticeId(Guid childId) => FlashCardPracticeId.ForChild(new UserId(childId));

    private static ProgressId ProgressIdOf(Guid childId) => ProgressId.ForChild(new UserId(childId));

    private async Task<DateOnly> TodayAsync(Guid childId)
    {
        var users = fixture.Host.Services.GetRequiredService<IUserEventStore>();

        return UserLocalDate.Today((await users.FindSnapshotAsync(new UserId(childId), CancellationToken.None))!, TimeProvider.System);
    }

    private async Task<IReadOnlyList<FlashCardStarsAwarded>> AwardsAsync(Guid childId) =>
        [.. (await Progress.ReadAsync(ProgressIdOf(childId), CancellationToken.None)).Select(e => e.Value).OfType<FlashCardStarsAwarded>()];

    private async Task<int> GameCountAsync(string guardianToken, Guid childId) =>
        (await GetChildAsync(fixture, guardianToken, childId)).Games.Count;

    [Fact]
    public async Task A_crash_after_the_award_is_recovered_by_a_retry_with_the_same_game_id()
    {
        using var meter = new FlashCardMeterDeltas();
        var (guardianToken, childId, childToken, set, enabled) = await SetUpAsync();
        var deck = await GetDeckAsync(fixture, childToken, set.Id, enabled.Tiers[0].Id);
        var gameId = Guid.NewGuid();
        TestFlashCardPracticeEventStore.FailNextFinishedAppends(childId, 1, new InvalidOperationException("injected crash"));

        await FinishAsync(fixture, childToken, gameId, set.Id, enabled.Tiers[0].Id, ResultsFor(deck), 500);

        // The premise: the stars are committed, the history entry is not.
        Assert.Equal(1, Assert.Single(await AwardsAsync(childId)).Stars);
        Assert.Null(await Practices.FindFinishedGameAsync(PracticeId(childId), gameId, CancellationToken.None));
        Assert.Equal(0, await GameCountAsync(guardianToken, childId));

        var retried = (await FinishAsync(fixture, childToken, gameId, set.Id, enabled.Tiers[0].Id, ResultsFor(deck))).ReadAsJson<FinishFlashCardGameResponse>();

        Assert.Equal(1, retried.Stars);
        Assert.Equal(1, retried.TotalStars);
        Assert.Single(await AwardsAsync(childId));
        Assert.Equal(1, await GameCountAsync(guardianToken, childId));
        Assert.Equal(1, meter.Finish(FlashCardFinishOutcome.Recovered));
        Assert.Equal(1, meter.Recovery);
        Assert.Equal(0, meter.Finish(FlashCardFinishOutcome.Awarded));
    }

    [Fact]
    public async Task A_crash_after_a_zero_award_retries_to_zero_and_one_history_row()
    {
        var (guardianToken, childId, childToken, set, enabled) = await SetUpAsync();
        await PlayAsync(fixture, childToken, set.Id, enabled.Tiers[0]);
        using var meter = new FlashCardMeterDeltas();
        var deck = await GetDeckAsync(fixture, childToken, set.Id, enabled.Tiers[0].Id);
        var gameId = Guid.NewGuid();
        TestFlashCardPracticeEventStore.FailNextFinishedAppends(childId, 1, new InvalidOperationException("injected crash"));

        await FinishAsync(fixture, childToken, gameId, set.Id, enabled.Tiers[0].Id, ResultsFor(deck), 500);
        var retried = (await FinishAsync(fixture, childToken, gameId, set.Id, enabled.Tiers[0].Id, ResultsFor(deck))).ReadAsJson<FinishFlashCardGameResponse>();

        Assert.Equal(0, retried.Stars);
        Assert.Single(await AwardsAsync(childId));
        Assert.Equal(2, await GameCountAsync(guardianToken, childId));
        Assert.Equal(1, meter.Finish(FlashCardFinishOutcome.Zero));
        Assert.Equal(0, meter.Recovery);
    }

    [Fact]
    public async Task A_lost_practice_append_after_the_award_is_409_and_the_retry_is_recovered()
    {
        using var meter = new FlashCardMeterDeltas();
        var (guardianToken, childId, childToken, set, enabled) = await SetUpAsync();
        var deck = await GetDeckAsync(fixture, childToken, set.Id, enabled.Tiers[1].Id);
        var gameId = Guid.NewGuid();
        TestFlashCardPracticeEventStore.FailNextFinishedAppends(childId, 1, new ConcurrencyException(typeof(FlashCardPractice), childId));

        var conflict = await FinishAsync(fixture, childToken, gameId, set.Id, enabled.Tiers[1].Id, ResultsFor(deck), 409);
        var retried = (await FinishAsync(fixture, childToken, gameId, set.Id, enabled.Tiers[1].Id, ResultsFor(deck))).ReadAsJson<FinishFlashCardGameResponse>();

        Assert.Contains("concurrency_conflict", conflict.ReadAsText());
        Assert.Equal(3, retried.Stars);
        Assert.Single(await AwardsAsync(childId));
        Assert.Equal(1, await GameCountAsync(guardianToken, childId));
        Assert.Equal(1, meter.Finish(FlashCardFinishOutcome.Recovered));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Stars_already_awarded_are_not_stranded_when_the_set_is_disabled_or_the_tier_removed_before_the_retry(bool removeTierInstead)
    {
        using var meter = new FlashCardMeterDeltas();
        var (guardianToken, childId, childToken, set, enabled) = await SetUpAsync();
        var deck = await GetDeckAsync(fixture, childToken, set.Id, enabled.Tiers[1].Id);
        var gameId = Guid.NewGuid();
        TestFlashCardPracticeEventStore.FailNextFinishedAppends(childId, 1, new ConcurrencyException(typeof(FlashCardPractice), childId));
        await FinishAsync(fixture, childToken, gameId, set.Id, enabled.Tiers[1].Id, ResultsFor(deck), 409);

        if (removeTierInstead)
        {
            await PutAsync(fixture, guardianToken, $"/flash-cards/children/{childId}/sets/{set.Id}", new { Tiers = new[] { new { Id = enabled.Tiers[0].Id, CardCount = 5, Stars = 1 } } });
        }
        else
        {
            await DeleteAsync(fixture, guardianToken, $"/flash-cards/children/{childId}/sets/{set.Id}");
        }

        var retried = (await FinishAsync(fixture, childToken, gameId, set.Id, enabled.Tiers[1].Id, ResultsFor(deck))).ReadAsJson<FinishFlashCardGameResponse>();

        Assert.Equal(3, retried.Stars);
        var history = Assert.Single((await GetChildAsync(fixture, guardianToken, childId)).Games);
        Assert.Equal(3, history.Stars);
        Assert.Single(await AwardsAsync(childId));
        Assert.Equal(1, meter.Finish(FlashCardFinishOutcome.Recovered));
    }

    [Fact]
    public async Task A_recovery_for_a_game_id_awarded_to_another_set_is_game_id_reused()
    {
        var (guardianToken, childId, childToken, set, enabled) = await SetUpAsync();
        var other = await CreatePlayableSetAsync(fixture, guardianToken, "Other");
        var otherEnabled = await EnableAsync(fixture, guardianToken, childId, other.Id);
        var deck = await GetDeckAsync(fixture, childToken, set.Id, enabled.Tiers[0].Id);
        var gameId = Guid.NewGuid();
        TestFlashCardPracticeEventStore.FailNextFinishedAppends(childId, 1, new InvalidOperationException("injected crash"));
        await FinishAsync(fixture, childToken, gameId, set.Id, enabled.Tiers[0].Id, ResultsFor(deck), 500);

        var error = (await FinishAsync(fixture, childToken, gameId, other.Id, otherEnabled.Tiers[0].Id, ResultsFor(deck), 422)).ReadAsText();

        Assert.Contains("game_id_reused", error);
    }

    // The snapshot is read after the version, so it is at that version or newer. A newer snapshot
    // that already holds the same game is a replay (200, nothing written); one that no longer offers
    // the set is a 422. Both decide on the newer data and never write.
    [Fact]
    public async Task A_finish_of_the_same_game_that_lands_between_the_version_and_the_snapshot_read_is_replayed()
    {
        using var meter = new FlashCardMeterDeltas();
        var (guardianToken, childId, childToken, set, enabled) = await SetUpAsync();
        var deck = await GetDeckAsync(fixture, childToken, set.Id, enabled.Tiers[0].Id);
        var gameId = Guid.NewGuid();
        var competing = new FlashCardGameFinished(
            PracticeId(childId), gameId, new FlashCardSetId(set.Id), set.Name, new FlashCardTierId(enabled.Tiers[0].Id), new DateOnly(2030, 1, 1), 5, [], 1, DateTimeOffset.UtcNow);
        TestFlashCardPracticeEventStore.OnNextSnapshotRead(childId, () => PracticeDecorator.AppendBehindTheTrackerAsync(PracticeId(childId), competing));

        var replayed = (await FinishAsync(fixture, childToken, gameId, set.Id, enabled.Tiers[0].Id, ResultsFor(deck))).ReadAsJson<FinishFlashCardGameResponse>();

        Assert.Equal(1, replayed.Stars);
        Assert.Equal(new DateOnly(2030, 1, 1), replayed.Game.FinishedOn);
        Assert.Equal(1, await GameCountAsync(guardianToken, childId));
        Assert.Empty(await AwardsAsync(childId));
        Assert.Equal(1, meter.Finish(FlashCardFinishOutcome.Replay));
    }

    [Fact]
    public async Task A_set_disabled_between_the_version_and_the_snapshot_read_is_422_and_awards_nothing()
    {
        var (_, childId, childToken, set, enabled) = await SetUpAsync();
        var deck = await GetDeckAsync(fixture, childToken, set.Id, enabled.Tiers[0].Id);
        var disabled = new FlashCardSetDisabled(PracticeId(childId), new FlashCardSetId(set.Id), new UserId(Guid.NewGuid()), DateTimeOffset.UtcNow);
        TestFlashCardPracticeEventStore.OnNextSnapshotRead(childId, () => PracticeDecorator.AppendBehindTheTrackerAsync(PracticeId(childId), disabled));

        var rejected = await FinishAsync(fixture, childToken, Guid.NewGuid(), set.Id, enabled.Tiers[0].Id, ResultsFor(deck), 422);

        Assert.Contains("game_no_longer_available", rejected.ReadAsText());
        Assert.Empty(await AwardsAsync(childId));
    }

    // The case the version-first order exists for: the newer snapshot still allows the game, so the
    // handler proceeds and awards -- and the append at the version it observed first must lose. Read
    // the snapshot first and this would append successfully and double-write config history.
    [Fact]
    public async Task A_config_change_between_the_version_and_the_snapshot_read_makes_the_append_lose_and_the_retry_recovers()
    {
        using var meter = new FlashCardMeterDeltas();
        var (guardianToken, childId, childToken, set, enabled) = await SetUpAsync();
        var deck = await GetDeckAsync(fixture, childToken, set.Id, enabled.Tiers[0].Id);
        var gameId = Guid.NewGuid();
        var unrelated = new FlashCardSetEnabled(
            PracticeId(childId), new FlashCardSetId(Guid.NewGuid()), "Unrelated", [new FlashCardTier(FlashCardTierId.New(), 5, 1)], new UserId(Guid.NewGuid()), DateTimeOffset.UtcNow);
        TestFlashCardPracticeEventStore.OnNextSnapshotRead(childId, () => PracticeDecorator.AppendBehindTheTrackerAsync(PracticeId(childId), unrelated));

        var conflict = await FinishAsync(fixture, childToken, gameId, set.Id, enabled.Tiers[0].Id, ResultsFor(deck), 409);

        Assert.Contains("concurrency_conflict", conflict.ReadAsText());
        Assert.Equal(0, await GameCountAsync(guardianToken, childId));

        var retried = (await FinishAsync(fixture, childToken, gameId, set.Id, enabled.Tiers[0].Id, ResultsFor(deck))).ReadAsJson<FinishFlashCardGameResponse>();

        Assert.Equal(1, retried.Stars);
        Assert.Single(await AwardsAsync(childId));
        Assert.Equal(1, await GameCountAsync(guardianToken, childId));
        Assert.Equal(1, meter.Finish(FlashCardFinishOutcome.Recovered));
    }

    [Fact]
    public async Task A_concurrent_award_on_an_existing_progress_stream_is_409_not_500_and_the_total_stays_one_award()
    {
        var (guardianToken, childId, childToken, set, enabled) = await SetUpAsync();
        // The stream must exist: a first finish for another set creates it.
        var other = await CreatePlayableSetAsync(fixture, guardianToken, "Other");
        var otherEnabled = await EnableAsync(fixture, guardianToken, childId, other.Id);
        await PlayAsync(fixture, childToken, other.Id, otherEnabled.Tiers[0]);
        var day = await TodayAsync(childId);
        var deck = await GetDeckAsync(fixture, childToken, set.Id, enabled.Tiers[1].Id);
        var gameId = Guid.NewGuid();
        ConcurrentWriterProgressEventStore.InterleaveNextReads(childId, 1, _ =>
            [new FlashCardStarsAwarded(ProgressIdOf(childId), set.Id, day, 3, Guid.NewGuid(), DateTimeOffset.UtcNow)]);

        var conflict = await FinishAsync(fixture, childToken, gameId, set.Id, enabled.Tiers[1].Id, ResultsFor(deck), 409);
        var retried = (await FinishAsync(fixture, childToken, gameId, set.Id, enabled.Tiers[1].Id, ResultsFor(deck))).ReadAsJson<FinishFlashCardGameResponse>();

        Assert.Contains("concurrency_conflict", conflict.ReadAsText());
        Assert.Equal(0, retried.Stars);
        // One award for the other set, one (the interleaved) for this set: never two for this set.
        Assert.Equal(1 + 3, await TotalStarsAsync(fixture, childToken));
        Assert.Equal(1, await GameCountAsync(guardianToken, childId) - 1);
    }

    [Fact]
    public async Task A_concurrent_stream_creation_is_409_not_500()
    {
        var (guardianToken, childId, childToken, set, enabled) = await SetUpAsync();
        var day = await TodayAsync(childId);
        var deck = await GetDeckAsync(fixture, childToken, set.Id, enabled.Tiers[1].Id);
        var gameId = Guid.NewGuid();
        ConcurrentWriterProgressEventStore.InterleaveNextReads(childId, 1, _ =>
        [
            new ProgressStarted(ProgressIdOf(childId), new UserId(childId), DateTimeOffset.UtcNow),
            new FlashCardStarsAwarded(ProgressIdOf(childId), set.Id, day, 3, Guid.NewGuid(), DateTimeOffset.UtcNow)
        ]);

        var conflict = await FinishAsync(fixture, childToken, gameId, set.Id, enabled.Tiers[1].Id, ResultsFor(deck), 409);
        var retried = (await FinishAsync(fixture, childToken, gameId, set.Id, enabled.Tiers[1].Id, ResultsFor(deck))).ReadAsJson<FinishFlashCardGameResponse>();

        Assert.Contains("concurrency_conflict", conflict.ReadAsText());
        Assert.Equal(0, retried.Stars);
        Assert.Equal(3, await TotalStarsAsync(fixture, childToken));
        Assert.Equal(1, await GameCountAsync(guardianToken, childId));
    }

    [Fact]
    public async Task A_retry_that_also_loses_the_race_is_409_again_and_the_third_attempt_wins()
    {
        var (guardianToken, childId, childToken, set, enabled) = await SetUpAsync();
        var day = await TodayAsync(childId);
        var deck = await GetDeckAsync(fixture, childToken, set.Id, enabled.Tiers[1].Id);
        var gameId = Guid.NewGuid();
        var firstWrite = true;
        // Each interleave is a finish of ANOTHER set, so this set's cap stays open and every retry
        // still has to append against a stale version.
        ConcurrentWriterProgressEventStore.InterleaveNextReads(childId, 2, _ =>
        {
            var events = new List<ProgressEvent>();

            if (firstWrite)
            {
                firstWrite = false;
                events.Add(new ProgressStarted(ProgressIdOf(childId), new UserId(childId), DateTimeOffset.UtcNow));
            }

            events.Add(new FlashCardStarsAwarded(ProgressIdOf(childId), Guid.NewGuid(), day, 2, Guid.NewGuid(), DateTimeOffset.UtcNow));

            return [.. events];
        });

        await FinishAsync(fixture, childToken, gameId, set.Id, enabled.Tiers[1].Id, ResultsFor(deck), 409);
        await FinishAsync(fixture, childToken, gameId, set.Id, enabled.Tiers[1].Id, ResultsFor(deck), 409);
        var third = (await FinishAsync(fixture, childToken, gameId, set.Id, enabled.Tiers[1].Id, ResultsFor(deck))).ReadAsJson<FinishFlashCardGameResponse>();

        Assert.Equal(3, third.Stars);
        Assert.Equal(2 + 2 + 3, await TotalStarsAsync(fixture, childToken));
        Assert.Equal(1, await GameCountAsync(guardianToken, childId));
    }

    [Fact]
    public async Task Two_sets_finishing_at_once_conflict_on_the_one_progress_stream_and_both_get_their_stars()
    {
        var (guardianToken, childId, childToken, set, enabled) = await SetUpAsync();
        var other = await CreatePlayableSetAsync(fixture, guardianToken, "Other");
        var otherEnabled = await EnableAsync(fixture, guardianToken, childId, other.Id);
        // Create the stream first so the interleave appends to an existing one.
        await PlayAsync(fixture, childToken, other.Id, otherEnabled.Tiers[0]);
        var day = await TodayAsync(childId);
        var deck = await GetDeckAsync(fixture, childToken, set.Id, enabled.Tiers[1].Id);
        var gameId = Guid.NewGuid();
        ConcurrentWriterProgressEventStore.InterleaveNextReads(childId, 1, _ =>
            [new FlashCardStarsAwarded(ProgressIdOf(childId), other.Id, day, 2, Guid.NewGuid(), DateTimeOffset.UtcNow)]);

        await FinishAsync(fixture, childToken, gameId, set.Id, enabled.Tiers[1].Id, ResultsFor(deck), 409);
        var retried = (await FinishAsync(fixture, childToken, gameId, set.Id, enabled.Tiers[1].Id, ResultsFor(deck))).ReadAsJson<FinishFlashCardGameResponse>();

        Assert.Equal(3, retried.Stars);
        Assert.Equal(1 + 2 + 3, await TotalStarsAsync(fixture, childToken));
    }

    [Fact]
    public async Task The_same_idempotency_key_in_flight_is_409_and_afterwards_replays_the_stored_body()
    {
        var (guardianToken, childId, childToken, set, enabled) = await SetUpAsync();
        var deck = await GetDeckAsync(fixture, childToken, set.Id, enabled.Tiers[0].Id);
        var gameId = Guid.NewGuid();
        var gate = TestFlashCardPracticeEventStore.GateNextFinishedAppend(childId);
        Task<Alba.IScenarioResult>? first = null;

        try
        {
            first = FinishAsync(fixture, childToken, gameId, set.Id, enabled.Tiers[0].Id, ResultsFor(deck), idempotencyKey: "same-key");
            await gate.Reached;

            var inFlight = await FinishAsync(fixture, childToken, gameId, set.Id, enabled.Tiers[0].Id, ResultsFor(deck), 409, "same-key");
            Assert.Contains("idempotency_key_in_progress", inFlight.ReadAsText());
        }
        finally
        {
            gate.Release();
        }

        var firstBody = (await first!.WaitAsync(TestFlashCardPracticeEventStore.Gate.Timeout)).ReadAsJson<FinishFlashCardGameResponse>();
        var stored = (await FinishAsync(fixture, childToken, gameId, set.Id, enabled.Tiers[0].Id, ResultsFor(deck), idempotencyKey: "same-key")).ReadAsJson<FinishFlashCardGameResponse>();

        Assert.Equal(firstBody, stored);
        Assert.Single(await AwardsAsync(childId));
        Assert.Equal(1, await GameCountAsync(guardianToken, childId));
    }

    [Fact]
    public async Task Two_simultaneous_finishes_award_exactly_one_tier()
    {
        var (_, childId, childToken, set, enabled) = await SetUpAsync();
        var deck = await GetDeckAsync(fixture, childToken, set.Id, enabled.Tiers[1].Id);

        // Different game ids, same set and day. Only the invariant is asserted, not who lost.
        var attempts = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            fixture.Host.Scenario(s =>
            {
                s.WithRequestHeader("Authorization", $"Bearer {childToken}");
                s.Post.Json(new { GameId = Guid.NewGuid(), SetId = set.Id, TierId = enabled.Tiers[1].Id, Results = ResultsFor(deck) }).ToUrl("/flash-cards/me/games");
                s.IgnoreStatusCode();
            })));

        Assert.All(attempts, a => Assert.True(a.Context.Response.StatusCode is 200 or 409, $"Unexpected status {a.Context.Response.StatusCode}."));
        Assert.Equal(3, await TotalStarsAsync(fixture, childToken));
        Assert.Single(await AwardsAsync(childId));
    }

    [Fact]
    public async Task The_game_id_lookup_is_a_scoped_event_query_not_a_stream_read_even_on_a_long_progress_stream()
    {
        var (guardianToken, childId, childToken, set, enabled) = await SetUpAsync();
        await SeedStarAwardsAsync(childId, 3000);
        var deck = await GetDeckAsync(fixture, childToken, set.Id, enabled.Tiers[0].Id);
        var gameId = Guid.NewGuid();
        TestFlashCardPracticeEventStore.FailNextFinishedAppends(childId, 1, new InvalidOperationException("injected crash"));
        await FinishAsync(fixture, childToken, gameId, set.Id, enabled.Tiers[0].Id, ResultsFor(deck), 500);
        ConcurrentWriterProgressEventStore.RecordLookups(childId);

        var started = System.Diagnostics.Stopwatch.StartNew();
        var retried = (await FinishAsync(fixture, childToken, gameId, set.Id, enabled.Tiers[0].Id, ResultsFor(deck))).ReadAsJson<FinishFlashCardGameResponse>();

        Console.WriteLine($"Recovery against a 3000-event progress stream took {started.ElapsedMilliseconds} ms.");
        Assert.Equal(1, retried.Stars);
        Assert.Equal(1, ConcurrentWriterProgressEventStore.LookupCount(childId));
        Assert.Equal(1, await GameCountAsync(guardianToken, childId));
    }

    [Fact]
    public async Task A_game_id_awarded_to_another_child_is_never_read_by_the_recovery_lookup()
    {
        var (_, childId, childToken, set, enabled) = await SetUpAsync();
        var (_, otherChildId, otherChildToken, otherSet, otherEnabled) = await SetUpAsync();
        var gameId = Guid.NewGuid();
        TestFlashCardPracticeEventStore.FailNextFinishedAppends(childId, 1, new InvalidOperationException("injected crash"));
        var deck = await GetDeckAsync(fixture, childToken, set.Id, enabled.Tiers[1].Id);
        await FinishAsync(fixture, childToken, gameId, set.Id, enabled.Tiers[1].Id, ResultsFor(deck), 500);

        // The other child's finish with the same GameId must not pick up the first child's 3 stars.
        var otherFinished = await PlayAsync(fixture, otherChildToken, otherSet.Id, otherEnabled.Tiers[0], gameId);

        Assert.Equal(1, otherFinished.Stars);
        Assert.Equal(1, otherFinished.TotalStars);
        Assert.Single(await AwardsAsync(otherChildId));
    }

    private async Task SeedStarAwardsAsync(Guid childId, int count)
    {
        var store = fixture.Host.Services.GetRequiredService<IProgressStore>();
        var id = ProgressIdOf(childId);
        var item = new buddy.Features.Calendars.CalendarItemId(Guid.NewGuid());

        await using var session = store.LightweightSession();
        session.Events.StartStream(
            id.Value,
            [
                (object)new ProgressStarted(id, new UserId(childId), DateTimeOffset.UtcNow),
                .. Enumerable.Range(0, count).Select(i => (object)new StarAwarded(id, item, new DateOnly(2020, 1, 1).AddDays(i), DateTimeOffset.UtcNow, new buddy.Features.Calendars.CompletionTarget.WholeTask()))
            ]);
        await session.SaveChangesAsync();
    }
}
