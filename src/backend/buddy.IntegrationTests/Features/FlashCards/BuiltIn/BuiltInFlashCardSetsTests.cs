using System.Collections.Immutable;

using buddy.Common;
using buddy.Features.FlashCards;
using buddy.Features.Users;
using buddy.IntegrationTests.Common.Concurrency;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.FlashCards.FlashCardTestHelpers;

namespace buddy.IntegrationTests.Features.FlashCards.BuiltIn;

// Built-in sets through the HTTP API, against FixtureBuiltInCatalogue (the shared host's catalogue).
[Collection(BuddyApiCollection.Name)]
public sealed class BuiltInFlashCardSetsTests(BuddyApiFixture fixture) : IAsyncLifetime
{
    private readonly List<Guid> _children = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        foreach (var child in _children)
        {
            TestFlashCardPracticeEventStore.Disarm(child);
        }

        return Task.CompletedTask;
    }

    private static Guid Clock => FixtureBuiltInCatalogue.ClockId.Value;

    private static Guid Flags => FixtureBuiltInCatalogue.FlagsId.Value;

    private static Guid Words => FixtureBuiltInCatalogue.WordsId.Value;

    private static Guid Empty => FixtureBuiltInCatalogue.EmptyId.Value;

    private async Task<(string GuardianToken, Guid GuardianId, Guid ChildId, string ChildToken, EnabledFlashCardSetResponse Enabled)> SetUpAsync(Guid setId)
    {
        var (guardianToken, guardianId, child, childToken) = await CreateFamilyAsync(fixture);
        var enabled = await EnableAsync(fixture, guardianToken, child.Id, setId);
        _children.Add(child.Id);

        return (guardianToken, guardianId, child.Id, childToken, enabled);
    }

    private static object[] Results(FlashCardDeckResponse deck, string? keyForSlotZero = null, bool echo = false) =>
        [.. deck.Cards.Select((card, slot) => new
        {
            Slot = slot,
            CardKey = slot == 0 && keyForSlotZero is not null ? keyForSlotZero : card.Key,
            KnewFirstTime = true,
            Question = echo ? "echoed question" : null,
            Answer = echo ? "echoed answer" : null
        })];

    private async Task<List<FlashCardSetResponse>> ListAsync(string token) =>
        (await GetAsync(fixture, token, "/flash-cards/sets")).ReadAsJson<List<FlashCardSetResponse>>()!;

    [Fact]
    public async Task The_guardians_library_lists_their_own_sets_and_then_the_catalogue()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var own = await CreateTextSetAsync(fixture, token, "Own");

        var sets = await ListAsync(token);

        Assert.Equal(own.Id, sets[0].Id);
        Assert.Equal(FixtureBuiltInCatalogue.Descriptors.Select(d => d.Id.Value), sets.Skip(1).Select(s => s.Id));
        var clock = sets.Single(s => s.Id == Clock);
        Assert.Equal(FlashCardSetKind.BuiltIn, clock.Kind);
        Assert.Equal("fixture:clock", clock.BuiltInKey);
        Assert.Equal("Fixture: urskive", clock.Name);
        Assert.Equal(3, clock.UniqueCardCount);
        Assert.Null(clock.OwnerId);
        Assert.Empty(clock.Cards);
        Assert.Null(clock.Math);
    }

    [Fact]
    public async Task Every_built_in_set_with_cards_has_an_example_card_and_one_without_has_none()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        var sets = await ListAsync(token);

        var clock = sets.Single(s => s.Id == Clock).ExampleCard!;
        Assert.Equal("clock:3:30", clock.Key);
        Assert.Equal(new DeckClockResponse(3, 30), clock.Clock);
        Assert.Equal("Hvad er klokken?", clock.Question);
        Assert.NotNull(sets.Single(s => s.Id == Flags).ExampleCard);
        Assert.Null(sets.Single(s => s.Id == Empty).ExampleCard);
        Assert.All(sets.Where(s => s.BuiltInKey is not null), s => Assert.Null(s.OwnerId));
        Assert.All(sets.Where(s => s.BuiltInKey is null), s => Assert.NotNull(s.OwnerId));
    }

    [Fact]
    public async Task A_guardians_erasure_leaves_the_catalogue_listed_for_everyone()
    {
        var (_, erasedToken, _) = await fixture.CreateAuthenticatedUserAsync();
        await CreateTextSetAsync(fixture, erasedToken, "Gone soon");
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {erasedToken}");
            _.Delete.Url("/users/me");
            _.StatusCodeShouldBe(204);
        });
        var (_, otherToken, _) = await fixture.CreateAuthenticatedUserAsync();

        Assert.Equal(FixtureBuiltInCatalogue.Descriptors.Count, (await ListAsync(otherToken)).Count(s => s.BuiltInKey is not null));
    }

    [Fact]
    public async Task A_built_in_set_can_be_enabled_for_a_child_and_shows_up_with_its_danish_name()
    {
        var (guardianToken, _, childId, childToken, enabled) = await SetUpAsync(Clock);

        Assert.Equal("Fixture: urskive", enabled.SetName);
        var practice = await GetChildAsync(fixture, guardianToken, childId);
        var listed = Assert.Single(practice.EnabledSets);
        Assert.Equal(Clock, listed.SetId);
        Assert.Equal("Fixture: urskive", listed.SetName);

        var mine = Assert.Single((await GetAsync(fixture, childToken, "/flash-cards/me/sets")).ReadAsJson<List<MyFlashCardSetResponse>>()!);
        Assert.Equal(FlashCardSetKind.BuiltIn, mine.Kind);
        Assert.Equal("fixture:clock", mine.BuiltInKey);
        Assert.Equal(3, mine.UniqueCardCount);
    }

    [Fact]
    public async Task Enabling_a_built_in_set_twice_is_idempotent_and_disabling_twice_is_fine()
    {
        var (guardianToken, _, childId, _, enabled) = await SetUpAsync(Flags);

        var again = await EnableAsync(fixture, guardianToken, childId, Flags, enabled.Tiers.Select(t => new { t.Id, t.CardCount, t.Stars }).ToArray());
        Assert.Equal(enabled.SetId, again.SetId);
        Assert.Equal(enabled.Tiers.Select(t => (t.Id, t.CardCount, t.Stars)), again.Tiers.Select(t => (t.Id, t.CardCount, t.Stars)));

        await DeleteAsync(fixture, guardianToken, $"/flash-cards/children/{childId}/sets/{Flags}");
        await DeleteAsync(fixture, guardianToken, $"/flash-cards/children/{childId}/sets/{Flags}");
        Assert.Empty((await GetChildAsync(fixture, guardianToken, childId)).EnabledSets);
    }

    [Fact]
    public async Task A_built_in_set_without_cards_cannot_be_enabled()
    {
        var (guardianToken, _, child, _) = await CreateFamilyAsync(fixture);

        await PutAsync(fixture, guardianToken, $"/flash-cards/children/{child.Id}/sets/{Empty}", new { Tiers = TwoTiers }, 400);
    }

    [Fact]
    public async Task An_unlinked_guardian_still_gets_not_found_when_enabling_a_built_in_set()
    {
        var (_, _, child, _) = await CreateFamilyAsync(fixture);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await PutAsync(fixture, strangerToken, $"/flash-cards/children/{child.Id}/sets/{Clock}", new { Tiers = TwoTiers }, 404);
    }

    [Fact]
    public async Task A_child_plays_a_built_in_deck_of_exactly_the_tier_size_with_repeats_when_the_set_is_small()
    {
        var (_, _, _, childToken, enabled) = await SetUpAsync(Clock);

        var deck = await GetDeckAsync(fixture, childToken, Clock, enabled.Tiers[1].Id);

        Assert.Equal(10, deck.Cards.Count);
        Assert.Equal(3, deck.Cards.Select(c => c.Key).Distinct().Count());
        Assert.All(deck.Cards, c =>
        {
            Assert.NotNull(c.Clock);
            Assert.Null(c.Math);
            Assert.Equal("Hvad er klokken?", c.Question);
        });
    }

    [Fact]
    public async Task A_deck_from_a_larger_set_has_the_tier_size_and_no_clock()
    {
        var (_, _, _, childToken, enabled) = await SetUpAsync(Flags);

        var deck = await GetDeckAsync(fixture, childToken, Flags, enabled.Tiers[0].Id);

        Assert.Equal(5, deck.Cards.Count);
        Assert.All(deck.Cards, c => Assert.Null(c.Clock));
    }

    [Fact]
    public async Task The_deck_of_a_built_in_set_that_is_not_enabled_is_not_found()
    {
        var (_, _, _, childToken) = await CreateFamilyAsync(fixture);

        await GetAsync(fixture, childToken, $"/flash-cards/me/sets/{Clock}/deck?tierId={Guid.NewGuid()}", 404);
    }

    [Fact]
    public async Task Finishing_a_built_in_game_pays_stars_once_per_day_with_a_top_up_and_records_the_danish_name()
    {
        var (guardianToken, _, childId, childToken, enabled) = await SetUpAsync(Flags);
        var small = enabled.Tiers[0];
        var big = enabled.Tiers[1];

        var first = await PlayAsync(fixture, childToken, Flags, small);
        Assert.Equal(1, first.Stars);
        Assert.Equal("Fixture: flag", first.Game.SetName);
        Assert.Equal(0, (await PlayAsync(fixture, childToken, Flags, small)).Stars);
        Assert.Equal(2, (await PlayAsync(fixture, childToken, Flags, big)).Stars);

        Assert.Equal(3, await TotalStarsAsync(fixture, childToken));
        var history = (await GetChildAsync(fixture, guardianToken, childId)).Games;
        Assert.Equal(3, history.Count);
        Assert.All(history, g => Assert.Equal("Fixture: flag", g.SetName));
    }

    [Fact]
    public async Task A_finish_stores_the_catalogue_text_not_the_clients()
    {
        var (_, _, childId, childToken, enabled) = await SetUpAsync(Clock);
        var deck = await GetDeckAsync(fixture, childToken, Clock, enabled.Tiers[0].Id);

        await FinishAsync(fixture, childToken, Guid.NewGuid(), Clock, enabled.Tiers[0].Id, Results(deck, echo: true));

        var events = await fixture.Host.Services.GetRequiredService<IFlashCardPracticeEventStore>()
            .ReadAsync(FlashCardPracticeId.ForChild(new UserId(childId)), CancellationToken.None);
        var finished = Assert.Single(events.Select(e => e.Value).OfType<FlashCardGameFinished>());
        Assert.All(finished.Results, r =>
        {
            Assert.Equal("Hvad er klokken?", r.Question);
            Assert.Contains("(", r.Answer, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task A_key_the_catalogue_never_had_is_rejected_even_when_the_request_echoes_text()
    {
        var (_, _, _, childToken, enabled) = await SetUpAsync(Flags);
        var deck = await GetDeckAsync(fixture, childToken, Flags, enabled.Tiers[0].Id);

        var response = await FinishAsync(fixture, childToken, Guid.NewGuid(), Flags, enabled.Tiers[0].Id, Results(deck, "forged:key", echo: true), 400);

        Assert.Contains("results[0].cardKey", response.ReadAsJson<ErrorEnvelope>()!.Details.Keys);
        Assert.Equal(0, await TotalStarsAsync(fixture, childToken));
    }

    [Fact]
    public async Task A_valid_key_of_another_built_in_set_is_rejected()
    {
        var (_, _, _, childToken, enabled) = await SetUpAsync(Flags);
        var deck = await GetDeckAsync(fixture, childToken, Flags, enabled.Tiers[0].Id);

        await FinishAsync(fixture, childToken, Guid.NewGuid(), Flags, enabled.Tiers[0].Id, Results(deck, "clock:3:30", echo: true), 400);
    }

    [Fact]
    public async Task A_retired_key_keeps_the_echo_fallback()
    {
        var (_, _, _, childToken, enabled) = await SetUpAsync(Flags);
        var deck = await GetDeckAsync(fixture, childToken, Flags, enabled.Tiers[0].Id);

        await FinishAsync(fixture, childToken, Guid.NewGuid(), Flags, enabled.Tiers[0].Id, Results(deck, FixtureBuiltInCatalogue.RetiredKey, echo: true));
        // Without an echo a retired key is as unusable as an unknown one.
        await FinishAsync(fixture, childToken, Guid.NewGuid(), Flags, enabled.Tiers[0].Id, Results(deck, FixtureBuiltInCatalogue.RetiredKey), 400);
    }

    [Fact]
    public async Task A_clock_key_never_reaches_the_guid_parse_and_a_repeated_key_across_slots_is_legal()
    {
        var (_, _, _, childToken, enabled) = await SetUpAsync(Clock);
        var deck = await GetDeckAsync(fixture, childToken, Clock, enabled.Tiers[0].Id);
        var repeated = deck.Cards.Select((_, slot) => new { Slot = slot, CardKey = "clock:3:30", KnewFirstTime = true }).ToArray();

        await FinishAsync(fixture, childToken, Guid.NewGuid(), Clock, enabled.Tiers[0].Id, repeated);
    }

    [Fact]
    public async Task A_finish_whose_result_count_differs_from_the_tier_is_rejected()
    {
        var (_, _, _, childToken, enabled) = await SetUpAsync(Flags);
        var deck = await GetDeckAsync(fixture, childToken, Flags, enabled.Tiers[0].Id);

        await FinishAsync(fixture, childToken, Guid.NewGuid(), Flags, enabled.Tiers[0].Id, Results(deck).Take(3).ToArray(), 400);
    }

    [Fact]
    public async Task Finishing_a_built_in_set_that_is_not_enabled_is_no_longer_available()
    {
        var (_, _, _, childToken) = await CreateFamilyAsync(fixture);

        var response = await FinishAsync(fixture, childToken, Guid.NewGuid(), Flags, Guid.NewGuid(), new[] { new { Slot = 0, CardKey = "flag:DK", KnewFirstTime = true } }, 422);

        Assert.Equal(FinishFlashCardGameOutcome.GameNoLongerAvailableCode, response.ReadAsJson<ErrorEnvelope>()!.Code);
    }

    [Fact]
    public async Task A_forged_key_in_the_recovery_branch_is_rejected_and_appends_no_game()
    {
        var (guardianToken, _, childId, childToken, enabled) = await SetUpAsync(Flags);
        var deck = await GetDeckAsync(fixture, childToken, Flags, enabled.Tiers[0].Id);
        var gameId = Guid.NewGuid();
        TestFlashCardPracticeEventStore.FailNextFinishedAppends(childId, 1, new InvalidOperationException("injected crash"));
        await FinishAsync(fixture, childToken, gameId, Flags, enabled.Tiers[0].Id, Results(deck), 500);

        await FinishAsync(fixture, childToken, gameId, Flags, enabled.Tiers[0].Id, Results(deck, "forged:key"), 400);

        Assert.Empty((await GetChildAsync(fixture, guardianToken, childId)).Games);
    }

    [Fact]
    public async Task The_kind_tag_is_builtin_on_finish_and_replay_and_on_a_recovered_finish()
    {
        var (_, _, childId, childToken, enabled) = await SetUpAsync(Flags);
        var deck = await GetDeckAsync(fixture, childToken, Flags, enabled.Tiers[0].Id);
        var gameId = Guid.NewGuid();
        using var meter = new FlashCardMeterDeltas();

        await FinishAsync(fixture, childToken, gameId, Flags, enabled.Tiers[0].Id, ResultsFor(deck));
        await FinishAsync(fixture, childToken, gameId, Flags, enabled.Tiers[0].Id, ResultsFor(deck));
        TestFlashCardPracticeEventStore.FailNextFinishedAppends(childId, 1, new InvalidOperationException("injected crash"));
        var crashed = await GetDeckAsync(fixture, childToken, Flags, enabled.Tiers[1].Id);
        var crashedGame = Guid.NewGuid();
        await FinishAsync(fixture, childToken, crashedGame, Flags, enabled.Tiers[1].Id, ResultsFor(crashed), 500);
        await FinishAsync(fixture, childToken, crashedGame, Flags, enabled.Tiers[1].Id, ResultsFor(crashed));

        Assert.Equal(1, meter.Finish(FlashCardFinishOutcome.Awarded, "builtin"));
        Assert.Equal(1, meter.Finish(FlashCardFinishOutcome.Replay, "builtin"));
        Assert.Equal(1, meter.Finish(FlashCardFinishOutcome.Recovered, "builtin"));
        Assert.Equal(0, meter.Finish(FlashCardFinishOutcome.Replay, "unknown"));
    }

    [Fact]
    public async Task A_replay_of_a_math_game_is_tagged_unknown_and_a_first_finish_math()
    {
        var (guardianToken, _, child, childToken) = await CreateFamilyAsync(fixture);
        var set = await CreateMathSetAsync(fixture, guardianToken);
        var enabled = await EnableAsync(fixture, guardianToken, child.Id, set.Id);
        var deck = await GetDeckAsync(fixture, childToken, set.Id, enabled.Tiers[0].Id);
        var gameId = Guid.NewGuid();
        using var meter = new FlashCardMeterDeltas();

        await FinishAsync(fixture, childToken, gameId, set.Id, enabled.Tiers[0].Id, ResultsFor(deck));
        await FinishAsync(fixture, childToken, gameId, set.Id, enabled.Tiers[0].Id, ResultsFor(deck));

        Assert.Equal(1, meter.Finish(FlashCardFinishOutcome.Awarded, "math"));
        Assert.Equal(1, meter.Finish(FlashCardFinishOutcome.Replay, "unknown"));
    }

    [Fact]
    public async Task A_text_games_first_finish_is_tagged_text()
    {
        var (guardianToken, _, child, childToken) = await CreateFamilyAsync(fixture);
        var set = await CreatePlayableSetAsync(fixture, guardianToken);
        var enabled = await EnableAsync(fixture, guardianToken, child.Id, set.Id);
        using var meter = new FlashCardMeterDeltas();

        await PlayAsync(fixture, childToken, set.Id, enabled.Tiers[0]);

        Assert.Equal(1, meter.Finish(FlashCardFinishOutcome.Awarded, "text"));
    }

    // Pinned existing behaviour: an enabled set id that is in neither the catalogue nor the store.
    [Fact]
    public async Task An_enabled_set_that_exists_nowhere_is_skipped_in_the_list_404_for_the_deck_and_no_longer_available_for_a_finish()
    {
        var (_, _, child, childToken) = await CreateFamilyAsync(fixture);
        var ghost = FlashCardSetId.New();
        var tier = new FlashCardTier(FlashCardTierId.New(), 5, 1);
        var practice = FlashCardPracticeId.ForChild(new UserId(child.Id));
        var now = DateTimeOffset.UtcNow;
        await fixture.Host.Services.GetRequiredService<IFlashCardPracticeEventStore>().CreateAsync(
            practice,
            [
                new FlashCardPracticeStarted(practice, new UserId(child.Id), now),
                new FlashCardSetEnabled(practice, ghost, "Ghost", [tier], new UserId(child.Id), now)
            ],
            CancellationToken.None);

        Assert.Empty((await GetAsync(fixture, childToken, "/flash-cards/me/sets")).ReadAsJson<List<MyFlashCardSetResponse>>()!);
        await GetAsync(fixture, childToken, $"/flash-cards/me/sets/{ghost.Value}/deck?tierId={tier.Id.Value}", 404);
        var response = await FinishAsync(fixture, childToken, Guid.NewGuid(), ghost.Value, tier.Id.Value, new[] { new { Slot = 0, CardKey = "x", KnewFirstTime = true } }, 422);
        Assert.Equal(FinishFlashCardGameOutcome.GameNoLongerAvailableCode, response.ReadAsJson<ErrorEnvelope>()!.Code);
    }

    [Fact]
    public async Task A_childs_export_with_a_built_in_set_enabled_works_and_names_the_set()
    {
        var (guardianToken, _, _, childToken, _) = await SetUpAsync(Flags);
        await PlayAsync(fixture, childToken, Flags, (await GetAsync(fixture, childToken, "/flash-cards/me/sets")).ReadAsJson<List<MyFlashCardSetResponse>>()![0].Tiers[0]);

        var export = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url("/users/me/export");
            _.StatusCodeShouldBe(200);
        });

        Assert.Contains("Fixture: flag", await export.ReadAsTextAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_system_owner_is_never_returned_as_an_owner()
    {
        var sets = fixture.Host.Services.GetRequiredService<IFlashCardSetEventStore>();

        Assert.Empty(await sets.ListIdsByOwnersAsync([BuiltInCatalogue.SystemOwnerId], CancellationToken.None));
    }

    [Fact]
    public async Task The_event_store_synthesises_a_catalogue_set_and_nothing_else()
    {
        var sets = fixture.Host.Services.GetRequiredService<IFlashCardSetEventStore>();

        var synthesised = await sets.FindSnapshotAsync(FixtureBuiltInCatalogue.ClockId, CancellationToken.None);

        Assert.Equal(FlashCardSetKind.BuiltIn, synthesised!.Kind);
        Assert.False(synthesised.IsDeleted);
        Assert.Equal(BuiltInCatalogue.SystemOwnerId, synthesised.OwnerId);
        Assert.Null(await sets.FindSnapshotAsync(FlashCardSetId.New(), CancellationToken.None));
        Assert.Empty(await sets.ReadAsync(FixtureBuiltInCatalogue.ClockId, CancellationToken.None));
    }

    [Fact]
    public async Task A_built_in_set_cannot_be_created_through_the_api()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        var response = await PostAsync(fixture, token, "/flash-cards/sets", new { Name = "Sneaky", Kind = "BuiltIn" }, 400);

        Assert.Equal("validation_error", response.ReadAsJson<ErrorEnvelope>()!.Code);
    }

    // Guard matrix, all five mutating endpoints: invalid body 400, child 403, a built-in id the
    // read-only validation result, a random unknown id 404, an unlinked guardian still 404 for a stored set.
    [Fact]
    public async Task Every_mutating_endpoint_rejects_a_built_in_id_for_a_guardian_and_a_child()
    {
        var (guardianToken, _, _, childToken) = await CreateFamilyAsync(fixture);
        var card = Guid.NewGuid();
        var set = Clock;

        (HttpVerb Verb, string Url, object? Body)[] calls =
        [
            (HttpVerb.Put, $"/flash-cards/sets/{set}", new { Name = "Renamed" }),
            (HttpVerb.Post, $"/flash-cards/sets/{set}/cards", new { Question = "q", Answer = "a" }),
            (HttpVerb.Put, $"/flash-cards/sets/{set}/cards/{card}", new { Question = "q", Answer = "a" }),
            (HttpVerb.Delete, $"/flash-cards/sets/{set}/cards/{card}", null),
            (HttpVerb.Delete, $"/flash-cards/sets/{set}", null)
        ];

        foreach (var (verb, url, body) in calls)
        {
            var response = await SendAsync(guardianToken, verb, url, body, 400);
            var envelope = response.ReadAsJson<ErrorEnvelope>()!;
            Assert.Equal("validation_error", envelope.Code);
            Assert.Contains("Built-in sets are read-only.", envelope.Details.SelectMany(d => d.Value));

            await SendAsync(childToken, verb, url, body, 403);
        }
    }

    [Fact]
    public async Task The_mutating_endpoints_keep_their_validator_stranger_and_unknown_id_statuses()
    {
        var (guardianToken, _, _, childToken) = await CreateFamilyAsync(fixture);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var stored = await CreatePlayableSetAsync(fixture, guardianToken);
        var unknown = Guid.CreateVersion7();
        var card = stored.Cards[0].Id;

        // Invalid body: the validator answers before anything else, even for a built-in id.
        await SendAsync(guardianToken, HttpVerb.Put, $"/flash-cards/sets/{Clock}", new { Name = "" }, 400);
        await SendAsync(guardianToken, HttpVerb.Post, $"/flash-cards/sets/{Clock}/cards", new { Question = "", Answer = "" }, 400);
        await SendAsync(guardianToken, HttpVerb.Put, $"/flash-cards/sets/{Clock}/cards/{card}", new { Question = "", Answer = "" }, 400);

        // A random unknown id is still 404.
        await SendAsync(guardianToken, HttpVerb.Put, $"/flash-cards/sets/{unknown}", new { Name = "x" }, 404);
        await SendAsync(guardianToken, HttpVerb.Post, $"/flash-cards/sets/{unknown}/cards", new { Question = "q", Answer = "a" }, 404);
        await SendAsync(guardianToken, HttpVerb.Put, $"/flash-cards/sets/{unknown}/cards/{card}", new { Question = "q", Answer = "a" }, 404);
        await SendAsync(guardianToken, HttpVerb.Delete, $"/flash-cards/sets/{unknown}/cards/{card}", null, 404);
        await SendAsync(guardianToken, HttpVerb.Delete, $"/flash-cards/sets/{unknown}", null, 404);

        // A stranger gets 404 for a stored set, a child 403 even for a stored one.
        await SendAsync(strangerToken, HttpVerb.Delete, $"/flash-cards/sets/{stored.Id}", null, 404);
        await SendAsync(childToken, HttpVerb.Delete, $"/flash-cards/sets/{stored.Id}", null, 403);
    }

    private enum HttpVerb
    {
        Put,
        Post,
        Delete
    }

    private async Task<Alba.IScenarioResult> SendAsync(string token, HttpVerb verb, string url, object? body, int status)
    {
        try
        {
            return await (verb switch
            {
                HttpVerb.Put => PutAsync(fixture, token, url, body!, status),
                HttpVerb.Post => PostAsync(fixture, token, url, body!, status),
                _ => DeleteAsync(fixture, token, url, status)
            });
        }
        catch (Alba.Exceptions.ScenarioAssertionException e)
        {
            throw new InvalidOperationException($"{verb} {url}: {e.Message}", e);
        }
    }
}
