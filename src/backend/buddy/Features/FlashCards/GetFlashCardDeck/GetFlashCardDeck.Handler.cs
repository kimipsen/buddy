using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;

namespace buddy.Features.FlashCards;

public static class GetFlashCardDeckHandler
{
    // Builds a deck of exactly tier.CardCount cards (repeats when the set has fewer unique cards)
    // from the child's enabled config and per-card practice weights. Reads the snapshot only and
    // persists nothing: an abandoned game leaves no trace.
    public static async Task<Result<IReadOnlyList<DeckCard>>> Handle(
        GetFlashCardDeck query,
        IFlashCardPracticeEventStore practices,
        IFlashCardSetEventStore sets,
        IGuardianLinkEventStore guardians,
        IBuiltInCatalogue catalogue,
        Random random,
        CancellationToken cancellationToken)
    {
        var access = await FlashCardAuthorization.CheckPlay(query.ChildId, guardians, cancellationToken);

        if (access != FlashCardAccess.Allowed)
        {
            return access.ToDeniedResult<IReadOnlyList<DeckCard>>();
        }

        var practice = await practices.FindSnapshotAsync(FlashCardPracticeId.ForChild(query.ChildId), cancellationToken);
        var tier = practice?.FindEnabledSet(query.SetId)?.FindTier(query.TierId);

        if (tier is null)
        {
            return new Result<IReadOnlyList<DeckCard>>.NotFound();
        }

        if (await sets.FindSnapshotAsync(query.SetId, cancellationToken) is not { IsDeleted: false } set)
        {
            return new Result<IReadOnlyList<DeckCard>>.NotFound();
        }

        if (set.UniqueCardCount < 1)
        {
            return new Result<IReadOnlyList<DeckCard>>.Validation(ValidationProblem.Of("The set has no cards to play."));
        }

        var weightOf = CardWeighting.WeightOf(practice!, set.Id);

        IReadOnlyList<DeckCard> deck = set.BuiltIn is not null
            ? [.. catalogue.Draw(set, tier.CardCount, random, c => weightOf(c.Key, c.Question, c.Answer)).Select(DeckCard.From)]
            : set.Math is { } rule
            ? [.. MathDeck.Draw(rule, tier.CardCount, random, c => weightOf(c.Key, c.Question, c.Answer)).Select(c => new DeckCard(c.Key, c.Question, c.Answer, new DeckMathResponse(c.Left, c.Operation, c.Right)))]
            : [.. CardDeck.Draw(set.Cards, tier.CardCount, random, c => weightOf(CardKey(c), c.Question, c.Answer)).Select(c => new DeckCard(CardKey(c), c.Question, c.Answer, null))];

        return new Result<IReadOnlyList<DeckCard>>.Success(deck);
    }

    // A guardian card's CardKey is its id; generated math cards carry their own (MathDeck).
    public static string CardKey(FlashCard card) => card.Id.Value.ToString();
}
