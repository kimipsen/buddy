namespace buddy.Features.FlashCards;

// A flat enum, not a union: a closed union does not round-trip in persisted events. The kind is
// fixed when the set is created.
public enum FlashCardSetKind
{
    Math,
    Text
}
