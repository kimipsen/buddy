namespace buddy.Features.FlashCards;

// A game length the guardian offers for one enabled set: a number of cards and the stars for
// finishing it.
public sealed record FlashCardTier(FlashCardTierId Id, int CardCount, int Stars);
