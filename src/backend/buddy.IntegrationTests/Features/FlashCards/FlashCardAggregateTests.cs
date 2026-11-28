using buddy.Features.FlashCards;
using buddy.Features.Users;

using Xunit;

namespace buddy.IntegrationTests.Features.FlashCards;

public sealed class FlashCardAggregateTests
{
    private static readonly UserId Guardian = new(Guid.CreateVersion7());
    private static readonly UserId Child = new(Guid.CreateVersion7());
    private static readonly DateTimeOffset Now = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static FlashCardSetEvent TextSetCreated(FlashCardSetId id) =>
        new FlashCardSetCreated(id, Guardian, "Capitals", FlashCardSetKind.Text, null, Now);

    [Fact]
    public void A_set_folds_cards_renames_and_a_soft_delete()
    {
        var id = FlashCardSetId.New();
        var a = new FlashCard(FlashCardId.New(), "Capital of France?", "Paris");
        var b = new FlashCard(FlashCardId.New(), "Capital of Italy?", "Rome");

        var set = FlashCardSet.Replay(
        [
            TextSetCreated(id),
            new FlashCardAdded(id, a, Guardian, Now),
            new FlashCardAdded(id, b, Guardian, Now),
            new FlashCardUpdated(id, a.Id, "Capital of Spain?", "Madrid", Guardian, Now),
            new FlashCardRemoved(id, b.Id, Guardian, Now),
            new FlashCardSetUpdated(id, "European capitals", null, Guardian, Now),
        ]);

        Assert.Equal("European capitals", set.Name);
        Assert.Equal([new FlashCard(a.Id, "Capital of Spain?", "Madrid")], set.Cards);
        Assert.Equal(1, set.UniqueCardCount);
        Assert.False(set.IsDeleted);

        var deleted = FlashCardSet.Advance(set, new FlashCardSetDeleted(id, Guardian, Now.AddDays(1)));

        Assert.True(deleted.IsDeleted);
        Assert.Equal(Now.AddDays(1), deleted.DeletedAt);
        Assert.Equal("European capitals", deleted.Name);
    }

    [Fact]
    public void A_math_set_counts_its_generated_cards_and_keeps_its_rule_on_a_rename()
    {
        var id = FlashCardSetId.New();

        var set = FlashCardSet.Replay(
        [
            new FlashCardSetCreated(id, Guardian, "Times 7", FlashCardSetKind.Math, new MathRule(MathOperation.Multiplication, 7), Now),
            new FlashCardSetUpdated(id, "Seven", null, Guardian, Now),
        ]);

        Assert.Equal(new MathRule(MathOperation.Multiplication, 7), set.Math);
        Assert.Equal(10, set.UniqueCardCount);

        var changed = FlashCardSet.Advance(set, new FlashCardSetUpdated(id, "Eight", new MathRule(MathOperation.Multiplication, 8), Guardian, Now));

        Assert.Equal(new MathRule(MathOperation.Multiplication, 8), changed.Math);
    }

    [Fact]
    public void A_set_rejects_a_card_event_for_an_unknown_card_and_a_second_start()
    {
        var id = FlashCardSetId.New();
        var set = FlashCardSet.Start(TextSetCreated(id));

        Assert.Throws<InvalidOperationException>(() => FlashCardSet.Advance(set, new FlashCardRemoved(id, FlashCardId.New(), Guardian, Now)));
        Assert.Throws<InvalidOperationException>(() => FlashCardSet.Advance(set, new FlashCardUpdated(id, FlashCardId.New(), "q", "a", Guardian, Now)));
        Assert.ThrowsAny<Exception>(() => FlashCardSet.Advance(set, TextSetCreated(id)));
        Assert.ThrowsAny<Exception>(() => FlashCardSet.Start(new FlashCardSetDeleted(id, Guardian, Now)));
    }

    [Fact]
    public void A_set_deserialised_without_cards_reads_them_as_empty()
    {
        var set = new FlashCardSet(FlashCardSetId.New(), Guardian, "x", FlashCardSetKind.Text, null, null!, false, null);

        Assert.Empty(set.Cards);
        Assert.Equal(0, set.UniqueCardCount);
    }

    private static FlashCardPracticeId PracticeId => FlashCardPracticeId.ForChild(Child);

    private static FlashCardTier Tier(int cards, int stars) => new(FlashCardTierId.New(), cards, stars);

    [Fact]
    public void Enabling_a_set_twice_replaces_its_tiers_in_place_and_disabling_removes_it()
    {
        var first = FlashCardSetId.New();
        var second = FlashCardSetId.New();
        var started = new FlashCardPracticeStarted(PracticeId, Child, Now);
        var tier = Tier(10, 1);

        var practice = FlashCardPractice.Replay(
        [
            started,
            new FlashCardSetEnabled(PracticeId, first, "One", [tier], Guardian, Now),
            new FlashCardSetEnabled(PracticeId, second, "Two", [Tier(5, 1)], Guardian, Now),
            new FlashCardSetEnabled(PracticeId, first, "One!", [tier, Tier(20, 2)], Guardian, Now),
        ]);

        Assert.Equal([first, second], practice.EnabledSets.Select(s => s.SetId));
        Assert.Equal("One!", practice.EnabledSets[0].SetName);
        Assert.Equal(2, practice.EnabledSets[0].Tiers.Count);
        Assert.Equal(tier, practice.FindEnabledSet(first)!.FindTier(tier.Id));
        Assert.Null(practice.FindEnabledSet(first)!.FindTier(FlashCardTierId.New()));

        var disabled = FlashCardPractice.Advance(practice, new FlashCardSetDisabled(PracticeId, first, Guardian, Now));

        Assert.Equal([second], disabled.EnabledSets.Select(s => s.SetId));
        Assert.Null(disabled.FindEnabledSet(first));
    }

    private static FlashCardGameFinished Game(Guid id, int day, int known, int stars) => new(
        PracticeId,
        id,
        FlashCardSetId.New(),
        "Set",
        FlashCardTierId.New(),
        new DateOnly(2025, 1, 1).AddDays(day),
        3,
        [.. Enumerable.Range(0, 3).Select(i => new FlashCardResult(i, $"k{i}", "q", "a", i < known, 1))],
        stars,
        Now);

    [Fact]
    public void A_finished_game_becomes_a_summary_without_its_per_card_results()
    {
        var gameId = Guid.CreateVersion7();
        var practice = FlashCardPractice.Start(new FlashCardPracticeStarted(PracticeId, Child, Now));

        var finished = FlashCardPractice.Advance(practice, Game(gameId, 0, 2, 3));

        var summary = Assert.Single(finished.Games);
        Assert.Equal(gameId, summary.GameId);
        Assert.Equal(3, summary.CardCount);
        Assert.Equal(2, summary.KnownCount);
        Assert.Equal(3, summary.Stars);
        Assert.Equal(summary, finished.FindGame(gameId));
        Assert.Null(finished.FindGame(Guid.NewGuid()));
    }

    [Fact]
    public void The_snapshot_keeps_only_the_last_100_games()
    {
        var games = Enumerable.Range(0, 105).Select(i => Game(Guid.CreateVersion7(), i, 0, 0)).ToArray();
        var practice = games.Aggregate(
            FlashCardPractice.Start(new FlashCardPracticeStarted(PracticeId, Child, Now)),
            (state, game) => FlashCardPractice.Advance(state, game));

        Assert.Equal(100, practice.Games.Count);
        Assert.Equal(games[5].GameId, practice.Games[0].GameId);
        Assert.Equal(games[^1].GameId, practice.Games[^1].GameId);
    }

    [Fact]
    public void A_practice_deserialised_without_lists_reads_them_as_empty()
    {
        var practice = new FlashCardPractice(PracticeId, Child, null!, null!, null!);
        var enabled = new EnabledFlashCardSet(FlashCardSetId.New(), "x", null!);

        Assert.Empty(practice.EnabledSets);
        Assert.Empty(practice.Games);
        Assert.Empty(practice.CardWeights);
        Assert.Empty(enabled.Tiers);
    }

    [Fact]
    public void A_practice_rejects_a_second_start()
    {
        var practice = FlashCardPractice.Start(new FlashCardPracticeStarted(PracticeId, Child, Now));

        Assert.ThrowsAny<Exception>(() => FlashCardPractice.Advance(practice, new FlashCardPracticeStarted(PracticeId, Child, Now)));
        Assert.ThrowsAny<Exception>(() => FlashCardPractice.Start(new FlashCardSetDisabled(PracticeId, FlashCardSetId.New(), Guardian, Now)));
    }

    [Fact]
    public void Rehydrate_returns_null_for_an_empty_stream()
    {
        Assert.Null(FlashCardPractice.Rehydrate([]));
        Assert.Null(FlashCardSet.Rehydrate([]));
    }
}
