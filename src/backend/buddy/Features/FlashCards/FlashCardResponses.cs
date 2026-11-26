using buddy.Features.Users;

namespace buddy.Features.FlashCards;

public sealed record FlashCardResponse(Guid Id, string Question, string Answer);

// Cards is empty for a Math set: its cards are generated from Math. UniqueCardCount is how many
// different cards a game can draw from.
public sealed record FlashCardSetResponse(
    Guid Id,
    Guid OwnerId,
    string Name,
    FlashCardSetKind Kind,
    MathRule? Math,
    IReadOnlyList<FlashCardResponse> Cards,
    int UniqueCardCount)
{
    public static FlashCardSetResponse FromSet(FlashCardSet set) => new(
        set.Id.Value,
        set.OwnerId.Value,
        set.Name,
        set.Kind,
        set.Math,
        [.. set.Cards.Select(c => new FlashCardResponse(c.Id.Value, c.Question, c.Answer))],
        set.UniqueCardCount);
}

public sealed record FlashCardTierResponse(Guid Id, int CardCount, int Stars);

public sealed record EnabledFlashCardSetResponse(Guid SetId, string SetName, IReadOnlyList<FlashCardTierResponse> Tiers)
{
    public static EnabledFlashCardSetResponse FromEnabled(EnabledFlashCardSet enabled) => new(
        enabled.SetId.Value,
        enabled.SetName,
        [.. enabled.Tiers.Select(t => new FlashCardTierResponse(t.Id.Value, t.CardCount, t.Stars))]);
}

public sealed record FlashCardGameResponse(Guid GameId, DateOnly FinishedOn, Guid SetId, string SetName, int CardCount, int KnownCount, int Stars)
{
    public static FlashCardGameResponse From(FlashCardGameSummary g) =>
        new(g.GameId, g.FinishedOn, g.SetId.Value, g.SetName, g.CardCount, g.KnownCount, g.Stars);
}

// What a child has been given to play: the enabled sets with their tiers, and the recent games.
public sealed record ChildFlashCardsResponse(
    Guid ChildId,
    IReadOnlyList<EnabledFlashCardSetResponse> EnabledSets,
    IReadOnlyList<FlashCardGameResponse> Games)
{
    public static ChildFlashCardsResponse Empty(UserId childId) => new(childId.Value, [], []);

    public static ChildFlashCardsResponse FromPractice(FlashCardPractice practice) => new(
        practice.ChildId.Value,
        [.. practice.EnabledSets.Select(EnabledFlashCardSetResponse.FromEnabled)],
        [.. practice.Games.Select(FlashCardGameResponse.From)]);
}

// One enabled, not-deleted set a child can play. Name and kind are the set's current ones.
public sealed record PlayableFlashCardSet(FlashCardSet Set, EnabledFlashCardSet Enabled);

public sealed record MyFlashCardSetResponse(
    Guid SetId,
    string SetName,
    FlashCardSetKind Kind,
    int UniqueCardCount,
    IReadOnlyList<FlashCardTierResponse> Tiers)
{
    public static MyFlashCardSetResponse From(PlayableFlashCardSet playable) => new(
        playable.Set.Id.Value,
        playable.Set.Name,
        playable.Set.Kind,
        playable.Set.UniqueCardCount,
        [.. playable.Enabled.Tiers.Select(t => new FlashCardTierResponse(t.Id.Value, t.CardCount, t.Stars))]);
}

// Operands and operator next to the rendered question, so the UI never parses text.
public sealed record DeckMathResponse(int Left, MathOperation Operation, int Right);

public sealed record DeckCard(string Key, string Question, string Answer, DeckMathResponse? Math);

// Exactly tier.CardCount cards, repeats included. Nothing is persisted and there is no game id: the
// client generates it when the game starts.
public sealed record FlashCardDeckResponse(IReadOnlyList<DeckCard> Cards);

public sealed record FinishFlashCardGameResponse(int Stars, int TotalStars, FlashCardGameResponse Game);
