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

public sealed record FlashCardGameResponse(Guid GameId, DateOnly FinishedOn, Guid SetId, string SetName, int CardCount, int KnownCount, int Stars);

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
        [.. practice.Games.Select(g => new FlashCardGameResponse(g.GameId, g.FinishedOn, g.SetId.Value, g.SetName, g.CardCount, g.KnownCount, g.Stars))]);
}
