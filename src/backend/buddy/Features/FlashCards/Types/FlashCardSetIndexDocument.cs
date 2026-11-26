namespace buddy.Features.FlashCards;

// Resolves "which sets does guardian X own" -- the library lists the caller's sets plus those of
// their co-guardians. Written once on FlashCardSetCreated, never updated afterwards (a soft delete
// keeps the row; readers check FlashCardSet.IsDeleted).
public sealed record FlashCardSetIndexDocument(Guid Id, Guid OwnerId);
