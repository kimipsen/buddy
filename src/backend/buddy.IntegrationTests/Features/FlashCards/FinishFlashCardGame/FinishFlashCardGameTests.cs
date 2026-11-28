using buddy.Common;
using buddy.Features.FlashCards;
using buddy.Features.Progress;
using buddy.Features.Users;
using buddy.IntegrationTests.Common.Concurrency;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.FlashCards.FlashCardTestHelpers;

namespace buddy.IntegrationTests.Features.FlashCards.FinishFlashCardGame;

// The "same day" steps use the real clock, so they can flake in the minutes around the child's local
// midnight; the day boundary itself is pinned with a fake clock in FinishFlashCardGameDayBoundaryTests.
[Collection(BuddyApiCollection.Name)]
public sealed class FinishFlashCardGameTests(BuddyApiFixture fixture) : IAsyncLifetime
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

    private async Task<(string GuardianToken, Guid ChildId, string ChildToken, FlashCardSetResponse Set, EnabledFlashCardSetResponse Enabled)> SetUpAsync(bool math = false)
    {
        var (guardianToken, _, child, childToken) = await CreateFamilyAsync(fixture);
        var set = math ? await CreateMathSetAsync(fixture, guardianToken) : await CreatePlayableSetAsync(fixture, guardianToken);
        var enabled = await EnableAsync(fixture, guardianToken, child.Id, set.Id);
        _children.Add(child.Id);

        return (guardianToken, child.Id, childToken, set, enabled);
    }

    private async Task<IReadOnlyCollection<FlashCardPracticeEvent>> PracticeEventsAsync(Guid childId) =>
        await Practices.ReadAsync(FlashCardPracticeId.ForChild(new UserId(childId)), CancellationToken.None);

    private static async Task<ErrorEnvelope> ErrorAsync(Alba.IScenarioResult result) => result.ReadAsJson<ErrorEnvelope>()!;

    [Fact]
    [CoversEndpoint("FinishFlashCardGame")]
    public async Task Finishing_a_game_awards_the_tiers_stars_and_records_it_in_the_history()
    {
        using var meter = new FlashCardMeterDeltas();
        var (guardianToken, childId, childToken, set, enabled) = await SetUpAsync();
        var deck = await GetDeckAsync(fixture, childToken, set.Id, enabled.Tiers[0].Id);
        var gameId = Guid.NewGuid();

        var finished = (await FinishAsync(fixture, childToken, gameId, set.Id, enabled.Tiers[0].Id, ResultsFor(deck, knownCount: 3)))
            .ReadAsJson<FinishFlashCardGameResponse>();

        Assert.Equal(1, finished.Stars);
        Assert.Equal(1, finished.TotalStars);
        Assert.Equal(gameId, finished.Game.GameId);
        Assert.Equal(5, finished.Game.CardCount);
        Assert.Equal(3, finished.Game.KnownCount);
        Assert.Equal(1, await TotalStarsAsync(fixture, childToken));

        var history = Assert.Single((await GetChildAsync(fixture, guardianToken, childId)).Games);
        Assert.Equal(finished.Game, history);
        Assert.Equal(1, meter.Finish(FlashCardFinishOutcome.Awarded));
        Assert.Equal(0, meter.Finish(FlashCardFinishOutcome.Zero) + meter.Finish(FlashCardFinishOutcome.Replay) + meter.Finish(FlashCardFinishOutcome.Recovered));
    }

    [Fact]
    public async Task The_stars_land_in_the_normal_balance_and_unlock_goal_posts()
    {
        var (_, _, childToken, set, enabled) = await SetUpAsync();

        var finished = await PlayAsync(fixture, childToken, set.Id, enabled.Tiers[1]);

        Assert.Equal(3, finished.Stars);
        var progress = (await GetAsync(fixture, childToken, "/progress/me")).ReadAsJson<ProgressSummary>();
        Assert.Equal(3, progress.TotalStars);
        Assert.Equal(3, progress.SpendableStars);
    }

    [Fact]
    public async Task Stars_are_earned_once_per_set_per_day_with_a_top_up_for_a_higher_tier()
    {
        var (_, _, childToken, set, enabled) = await SetUpAsync();
        var small = enabled.Tiers[0];
        var big = enabled.Tiers[1];

        Assert.Equal(1, (await PlayAsync(fixture, childToken, set.Id, small)).Stars);
        Assert.Equal(0, (await PlayAsync(fixture, childToken, set.Id, small)).Stars);
        Assert.Equal(2, (await PlayAsync(fixture, childToken, set.Id, big)).Stars);
        Assert.Equal(0, (await PlayAsync(fixture, childToken, set.Id, big)).Stars);
        Assert.Equal(0, (await PlayAsync(fixture, childToken, set.Id, small)).Stars);

        Assert.Equal(3, await TotalStarsAsync(fixture, childToken));
    }

    [Fact]
    public async Task Each_set_has_its_own_daily_cap_and_every_game_is_in_the_history()
    {
        var (guardianToken, childId, childToken, set, enabled) = await SetUpAsync();
        var second = await CreatePlayableSetAsync(fixture, guardianToken, "Second");
        var secondEnabled = await EnableAsync(fixture, guardianToken, childId, second.Id);

        Assert.Equal(1, (await PlayAsync(fixture, childToken, set.Id, enabled.Tiers[0])).Stars);
        Assert.Equal(1, (await PlayAsync(fixture, childToken, second.Id, secondEnabled.Tiers[0])).Stars);
        Assert.Equal(0, (await PlayAsync(fixture, childToken, set.Id, enabled.Tiers[0])).Stars);

        Assert.Equal(3, (await GetChildAsync(fixture, guardianToken, childId)).Games.Count);
        Assert.Equal(2, await TotalStarsAsync(fixture, childToken));
    }

    [Fact]
    public async Task A_math_game_records_server_side_question_and_answer_text_per_slot()
    {
        var (_, childId, childToken, set, enabled) = await SetUpAsync(math: true);
        var deck = await GetDeckAsync(fixture, childToken, set.Id, enabled.Tiers[0].Id);

        await FinishAsync(fixture, childToken, Guid.NewGuid(), set.Id, enabled.Tiers[0].Id, ResultsFor(deck, knownCount: 2));

        var finished = (await PracticeEventsAsync(childId)).Select(e => e.Value).OfType<FlashCardGameFinished>().Single();
        Assert.Equal(5, finished.Results.Length);
        Assert.Equal(deck.Cards.Select(c => (c.Key, c.Question, c.Answer)), finished.Results.Select(r => (r.CardKey, r.Question, r.Answer)));
        Assert.Equal([true, true, false, false, false], finished.Results.Select(r => r.KnewFirstTime));
        Assert.Equal([1, 1, 2, 2, 2], finished.Results.Select(r => r.TimesShown));
    }

    [Fact]
    public async Task A_replayed_game_id_returns_the_stored_result_and_writes_nothing()
    {
        var (guardianToken, childId, childToken, set, enabled) = await SetUpAsync();
        var deck = await GetDeckAsync(fixture, childToken, set.Id, enabled.Tiers[0].Id);
        var gameId = Guid.NewGuid();

        var first = (await FinishAsync(fixture, childToken, gameId, set.Id, enabled.Tiers[0].Id, ResultsFor(deck), idempotencyKey: "k1"))
            .ReadAsJson<FinishFlashCardGameResponse>();
        var practiceId = FlashCardPracticeId.ForChild(new UserId(childId));
        var weightsAfterFirstFinish = (await Practices.FindSnapshotAsync(practiceId, CancellationToken.None))!.CardWeights;
        // New key, same game, and results that differ: the stored game decides.
        var second = (await FinishAsync(fixture, childToken, gameId, set.Id, enabled.Tiers[0].Id, ResultsFor(deck, knownCount: 0), idempotencyKey: "k2"))
            .ReadAsJson<FinishFlashCardGameResponse>();
        var weightsAfterReplay = (await Practices.FindSnapshotAsync(practiceId, CancellationToken.None))!.CardWeights;

        Assert.Equal(first, second);
        Assert.Equal(weightsAfterFirstFinish, weightsAfterReplay);
        Assert.Equal(1, await TotalStarsAsync(fixture, childToken));
        Assert.Single((await GetChildAsync(fixture, guardianToken, childId)).Games);
    }

    [Fact]
    public async Task A_replay_still_returns_the_stored_result_after_the_set_was_disabled()
    {
        var (guardianToken, childId, childToken, set, enabled) = await SetUpAsync();
        var gameId = Guid.NewGuid();
        var first = await PlayAsync(fixture, childToken, set.Id, enabled.Tiers[0], gameId);
        await DeleteAsync(fixture, guardianToken, $"/flash-cards/children/{childId}/sets/{set.Id}");

        var deck = new FlashCardDeckResponse([new DeckCard(set.Cards[0].Id.ToString(), "", "", null)]);
        var replay = (await FinishAsync(fixture, childToken, gameId, set.Id, enabled.Tiers[0].Id, ResultsFor(deck))).ReadAsJson<FinishFlashCardGameResponse>();

        Assert.Equal(first.Stars, replay.Stars);
        Assert.Equal(first.Game, replay.Game);
    }

    [Fact]
    public async Task A_game_id_used_for_another_set_or_tier_is_reused_and_rejected_with_422()
    {
        var (guardianToken, childId, childToken, set, enabled) = await SetUpAsync();
        var other = await CreatePlayableSetAsync(fixture, guardianToken, "Other");
        var otherEnabled = await EnableAsync(fixture, guardianToken, childId, other.Id);
        var gameId = Guid.NewGuid();
        await PlayAsync(fixture, childToken, set.Id, enabled.Tiers[0], gameId);
        var deck = await GetDeckAsync(fixture, childToken, set.Id, enabled.Tiers[0].Id);

        var otherTier = await ErrorAsync(await FinishAsync(fixture, childToken, gameId, set.Id, enabled.Tiers[1].Id, ResultsFor(deck), 422));
        var otherSet = await ErrorAsync(await FinishAsync(fixture, childToken, gameId, other.Id, otherEnabled.Tiers[0].Id, ResultsFor(deck), 422));

        Assert.Equal("game_id_reused", otherTier.Code);
        Assert.Equal("game_id_reused", otherSet.Code);
        Assert.Single((await GetChildAsync(fixture, guardianToken, childId)).Games);
    }

    [Fact]
    public async Task Another_child_posting_the_same_game_id_plays_an_independent_game()
    {
        var (_, _, childToken, set, enabled) = await SetUpAsync();
        var (otherGuardianToken, _, otherChild, otherChildToken) = await CreateFamilyAsync(fixture);
        var otherSet = await CreatePlayableSetAsync(fixture, otherGuardianToken);
        var otherEnabled = await EnableAsync(fixture, otherGuardianToken, otherChild.Id, otherSet.Id);
        var gameId = Guid.NewGuid();

        var first = await PlayAsync(fixture, childToken, set.Id, enabled.Tiers[1], gameId);
        var second = await PlayAsync(fixture, otherChildToken, otherSet.Id, otherEnabled.Tiers[0], gameId);

        Assert.Equal(3, first.Stars);
        Assert.Equal(1, second.Stars);
        Assert.Equal(otherSet.Id, second.Game.SetId);
        Assert.Equal(1, second.TotalStars);
        Assert.Single((await GetChildAsync(fixture, otherGuardianToken, otherChild.Id)).Games);
    }

    [Fact]
    public async Task A_guardian_posting_a_finish_gets_not_found()
    {
        var (guardianToken, _, childToken, set, enabled) = await SetUpAsync();
        var deck = await GetDeckAsync(fixture, childToken, set.Id, enabled.Tiers[0].Id);

        await FinishAsync(fixture, guardianToken, Guid.NewGuid(), set.Id, enabled.Tiers[0].Id, ResultsFor(deck), 404);
    }

    [Fact]
    public async Task A_child_with_no_practice_stream_gets_game_no_longer_available()
    {
        var (_, _, _, childToken) = await CreateFamilyAsync(fixture);

        var error = await ErrorAsync(await FinishAsync(fixture, childToken, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new[] { new { Slot = 0, CardKey = "x", KnewFirstTime = true } }, 422));

        Assert.Equal("game_no_longer_available", error.Code);
    }

    [Fact]
    public async Task A_finish_that_does_not_fit_the_request_shape_is_rejected_with_400()
    {
        var (_, _, childToken, set, enabled) = await SetUpAsync();
        var deck = await GetDeckAsync(fixture, childToken, set.Id, enabled.Tiers[0].Id);
        var tier = enabled.Tiers[0].Id;
        var key = deck.Cards[0].Key;

        await FinishAsync(fixture, childToken, Guid.Empty, set.Id, tier, ResultsFor(deck), 400);
        await FinishAsync(fixture, childToken, Guid.NewGuid(), set.Id, tier, Array.Empty<object>(), 400);
        // Duplicate slot, missing slot 1.
        await FinishAsync(fixture, childToken, Guid.NewGuid(), set.Id, tier, new[] { new { Slot = 0, CardKey = key, KnewFirstTime = true }, new { Slot = 0, CardKey = key, KnewFirstTime = true } }, 400);
        // Echo text out of bounds: empty and over the card limit.
        await FinishAsync(fixture, childToken, Guid.NewGuid(), set.Id, tier, new[] { new { Slot = 0, CardKey = key, KnewFirstTime = true, Question = "", Answer = "a" } }, 400);
        await FinishAsync(fixture, childToken, Guid.NewGuid(), set.Id, tier, new[] { new { Slot = 0, CardKey = key, KnewFirstTime = true, Question = "q", Answer = new string('a', FlashCardLimits.TextMaxLength + 1) } }, 400);
    }

    [Fact]
    public async Task A_result_count_other_than_the_tiers_card_count_is_rejected_with_400_and_awards_nothing()
    {
        var (_, _, childToken, set, enabled) = await SetUpAsync();
        var deck = await GetDeckAsync(fixture, childToken, set.Id, enabled.Tiers[1].Id);

        // The 10-card deck against the 5-card tier.
        await FinishAsync(fixture, childToken, Guid.NewGuid(), set.Id, enabled.Tiers[0].Id, ResultsFor(deck), 400);

        Assert.Equal(0, await TotalStarsAsync(fixture, childToken));
    }

    [Fact]
    public async Task A_card_key_that_does_not_belong_to_the_set_is_rejected_with_400()
    {
        var (_, _, childToken, set, enabled) = await SetUpAsync(math: true);
        var deck = await GetDeckAsync(fixture, childToken, set.Id, enabled.Tiers[0].Id);
        object[] results = [.. ResultsFor(deck).Take(4), new { Slot = 4, CardKey = "mul:7x3", KnewFirstTime = true }];

        await FinishAsync(fixture, childToken, Guid.NewGuid(), set.Id, enabled.Tiers[0].Id, results, 400);
        Assert.Equal(0, await TotalStarsAsync(fixture, childToken));
    }

    [Fact]
    public async Task An_edited_card_is_recorded_with_its_current_text_and_the_echo_is_ignored()
    {
        var (guardianToken, childId, childToken, set, enabled) = await SetUpAsync();
        var deck = await GetDeckAsync(fixture, childToken, set.Id, enabled.Tiers[0].Id);
        await PutAsync(fixture, guardianToken, $"/flash-cards/sets/{set.Id}/cards/{set.Cards[0].Id}", new { Question = "Capital of Sweden?", Answer = "Stockholm" });
        object[] results = [.. deck.Cards.Select((c, slot) => new { Slot = slot, CardKey = c.Key, KnewFirstTime = true, Question = "tampered", Answer = "tampered" })];

        var finished = (await FinishAsync(fixture, childToken, Guid.NewGuid(), set.Id, enabled.Tiers[0].Id, results)).ReadAsJson<FinishFlashCardGameResponse>();

        Assert.Equal(1, finished.Stars);
        var stored = (await PracticeEventsAsync(childId)).Select(e => e.Value).OfType<FlashCardGameFinished>().Single();
        Assert.All(stored.Results, r =>
        {
            Assert.Equal("Capital of Sweden?", r.Question);
            Assert.Equal("Stockholm", r.Answer);
        });
    }

    [Fact]
    public async Task A_card_removed_mid_game_still_records_the_game_with_the_echoed_text()
    {
        var (guardianToken, childId, childToken, set, enabled) = await SetUpAsync();
        var second = await AddCardAsync(fixture, guardianToken, set.Id, "Capital of Norway?", "Oslo");
        var deck = await GetDeckAsync(fixture, childToken, set.Id, enabled.Tiers[0].Id);
        // Remove whichever card the deck's first slot holds.
        var removedKey = deck.Cards[0].Key;
        await DeleteAsync(fixture, guardianToken, $"/flash-cards/sets/{set.Id}/cards/{removedKey}");
        object[] results = [.. deck.Cards.Select((c, slot) => new { Slot = slot, CardKey = c.Key, KnewFirstTime = true, Question = c.Question, Answer = c.Answer })];

        var finished = (await FinishAsync(fixture, childToken, Guid.NewGuid(), set.Id, enabled.Tiers[0].Id, results)).ReadAsJson<FinishFlashCardGameResponse>();

        Assert.Equal(1, finished.Stars);
        var stored = (await PracticeEventsAsync(childId)).Select(e => e.Value).OfType<FlashCardGameFinished>().Single();
        var removedResult = stored.Results.First(r => r.CardKey == removedKey);
        Assert.Equal(deck.Cards[0].Question, removedResult.Question);
        Assert.NotEmpty(second.Cards);
    }

    [Fact]
    public async Task A_set_deleted_mid_game_still_awards_the_stars_and_records_the_game()
    {
        var (guardianToken, childId, childToken, set, enabled) = await SetUpAsync();
        var deck = await GetDeckAsync(fixture, childToken, set.Id, enabled.Tiers[0].Id);
        await DeleteAsync(fixture, guardianToken, $"/flash-cards/sets/{set.Id}");

        var finished = (await FinishAsync(fixture, childToken, Guid.NewGuid(), set.Id, enabled.Tiers[0].Id, ResultsFor(deck))).ReadAsJson<FinishFlashCardGameResponse>();

        Assert.Equal(1, finished.Stars);
        Assert.Single((await GetChildAsync(fixture, guardianToken, childId)).Games);
    }

    [Fact]
    public async Task A_set_disabled_or_a_tier_removed_mid_game_is_422_and_writes_nothing()
    {
        var (guardianToken, childId, childToken, set, enabled) = await SetUpAsync();
        var deck = await GetDeckAsync(fixture, childToken, set.Id, enabled.Tiers[0].Id);
        var big = await GetDeckAsync(fixture, childToken, set.Id, enabled.Tiers[1].Id);

        // Remove the 10-card tier, keeping the 5-card one's id.
        await PutAsync(fixture, guardianToken, $"/flash-cards/children/{childId}/sets/{set.Id}", new { Tiers = new[] { new { Id = enabled.Tiers[0].Id, CardCount = 5, Stars = 1 } } });
        var removedTier = await ErrorAsync(await FinishAsync(fixture, childToken, Guid.NewGuid(), set.Id, enabled.Tiers[1].Id, ResultsFor(big), 422));

        await DeleteAsync(fixture, guardianToken, $"/flash-cards/children/{childId}/sets/{set.Id}");
        var disabled = await ErrorAsync(await FinishAsync(fixture, childToken, Guid.NewGuid(), set.Id, enabled.Tiers[0].Id, ResultsFor(deck), 422));

        Assert.Equal("game_no_longer_available", removedTier.Code);
        Assert.Equal("game_no_longer_available", disabled.Code);
        Assert.Equal(0, await TotalStarsAsync(fixture, childToken));
        Assert.Empty((await GetChildAsync(fixture, guardianToken, childId)).Games);
    }

    [Fact]
    public async Task Changing_an_unrelated_tier_keeps_the_other_tiers_id_so_a_game_in_progress_still_finishes()
    {
        var (guardianToken, childId, childToken, set, enabled) = await SetUpAsync();
        var deck = await GetDeckAsync(fixture, childToken, set.Id, enabled.Tiers[0].Id);

        await PutAsync(fixture, guardianToken, $"/flash-cards/children/{childId}/sets/{set.Id}", new
        {
            Tiers = new object[]
            {
                new { Id = enabled.Tiers[0].Id, CardCount = 5, Stars = 1 },
                new { Id = enabled.Tiers[1].Id, CardCount = 10, Stars = 4 }
            }
        });

        var finished = (await FinishAsync(fixture, childToken, Guid.NewGuid(), set.Id, enabled.Tiers[0].Id, ResultsFor(deck))).ReadAsJson<FinishFlashCardGameResponse>();

        Assert.Equal(1, finished.Stars);
    }

    [Fact]
    public async Task A_tier_whose_stars_changed_mid_game_awards_the_current_stars()
    {
        var (guardianToken, childId, childToken, set, enabled) = await SetUpAsync();
        var deck = await GetDeckAsync(fixture, childToken, set.Id, enabled.Tiers[0].Id);

        await PutAsync(fixture, guardianToken, $"/flash-cards/children/{childId}/sets/{set.Id}", new
        {
            Tiers = new object[]
            {
                new { Id = enabled.Tiers[0].Id, CardCount = 5, Stars = 7 },
                new { Id = enabled.Tiers[1].Id, CardCount = 10, Stars = 3 }
            }
        });

        var finished = (await FinishAsync(fixture, childToken, Guid.NewGuid(), set.Id, enabled.Tiers[0].Id, ResultsFor(deck))).ReadAsJson<FinishFlashCardGameResponse>();

        Assert.Equal(7, finished.Stars);
    }

    [Fact]
    public async Task Finish_reads_the_snapshot_and_the_version_but_never_the_practice_stream()
    {
        var (_, childId, childToken, set, enabled) = await SetUpAsync();
        // A long history: a few hundred finished games of 50 results each, seeded in one session.
        await SeedGamesAsync(childId, set.Id, enabled.Tiers[0].Id, games: 300);
        var deck = await GetDeckAsync(fixture, childToken, set.Id, enabled.Tiers[0].Id);
        TestFlashCardPracticeEventStore.Record(childId);

        var started = System.Diagnostics.Stopwatch.StartNew();
        await FinishAsync(fixture, childToken, Guid.NewGuid(), set.Id, enabled.Tiers[0].Id, ResultsFor(deck));

        Console.WriteLine($"Finish against a 300-game practice stream took {started.ElapsedMilliseconds} ms.");
        var calls = TestFlashCardPracticeEventStore.Calls(childId);
        Assert.DoesNotContain(nameof(IFlashCardPracticeEventStore.ReadAsync), calls);
        Assert.Contains(nameof(IFlashCardPracticeEventStore.ReadVersionAsync), calls);
        Assert.True(
            calls.ToList().IndexOf(nameof(IFlashCardPracticeEventStore.ReadVersionAsync)) < calls.ToList().IndexOf(nameof(IFlashCardPracticeEventStore.FindSnapshotAsync)),
            "The version must be read before the snapshot.");
    }

    [Fact]
    public async Task A_game_older_than_the_snapshot_window_is_still_replayed_by_the_typed_event_query()
    {
        var (_, childId, childToken, set, enabled) = await SetUpAsync();
        var oldGameId = Guid.NewGuid();
        var first = await PlayAsync(fixture, childToken, set.Id, enabled.Tiers[0], oldGameId);
        await SeedGamesAsync(childId, set.Id, enabled.Tiers[0].Id, games: FlashCardPractice.MaxGames + 5);
        var practice = await Practices.FindSnapshotAsync(FlashCardPracticeId.ForChild(new UserId(childId)), CancellationToken.None);
        Assert.Null(practice!.FindGame(oldGameId));
        TestFlashCardPracticeEventStore.Record(childId);

        var deck = await GetDeckAsync(fixture, childToken, set.Id, enabled.Tiers[0].Id);
        var replay = (await FinishAsync(fixture, childToken, oldGameId, set.Id, enabled.Tiers[0].Id, ResultsFor(deck))).ReadAsJson<FinishFlashCardGameResponse>();

        Assert.Equal(first.Game, replay.Game);
        Assert.Equal(first.Stars, replay.Stars);
        Assert.Contains(nameof(IFlashCardPracticeEventStore.FindFinishedGameAsync), TestFlashCardPracticeEventStore.Calls(childId));
        Assert.DoesNotContain(nameof(IFlashCardPracticeEventStore.ReadAsync), TestFlashCardPracticeEventStore.Calls(childId));
    }

    // Appends finished games straight through Marten, bypassing the tracker, in one session.
    private async Task SeedGamesAsync(Guid childId, Guid setId, Guid tierId, int games)
    {
        var id = FlashCardPracticeId.ForChild(new UserId(childId));
        var store = fixture.Host.Services.GetRequiredService<IFlashCardsStore>();
        var results = System.Collections.Immutable.ImmutableArray.CreateRange(
            Enumerable.Range(0, 50).Select(slot => new FlashCardResult(slot, "k", new string('q', 100), new string('a', 100), true, 1)));

        await using var session = store.LightweightSession();
        session.Events.Append(
            id.Value,
            Enumerable.Range(0, games)
                .Select(_ => (object)new FlashCardGameFinished(id, Guid.NewGuid(), new FlashCardSetId(setId), "Seeded", new FlashCardTierId(tierId), new DateOnly(2020, 1, 1), 50, results, 0, DateTimeOffset.UtcNow))
                .ToArray());
        await session.SaveChangesAsync();
    }
}
