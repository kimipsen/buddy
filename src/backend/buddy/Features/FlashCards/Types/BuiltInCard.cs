namespace buddy.Features.FlashCards;

// One card of a built-in set. Question and Answer are what results and history store as text
// (docs: implementation plan, D6); Clock is set only on a clock card, whose front is drawn from it.
public sealed record BuiltInCard(string Key, string Question, string Answer, DeckClockResponse? Clock = null);

// What the event store copies onto a synthesised built-in FlashCardSet: FlashCardSet is a plain
// record with no access to the catalogue, so everything the responses need travels here.
public sealed record BuiltInSetRef(string Key, int UniqueCardCount, BuiltInCard? Example);

// The three outcomes of resolving a stored card key against a built-in set (D9).
public enum BuiltInCardLookupKind
{
    Found,
    // Removed from the catalogue on purpose (BuiltInCatalogue retired keys): a stale client may still send it.
    Retired,
    // Never part of the catalogue: forged, or a key of another set.
    Unknown
}

public readonly record struct BuiltInCardLookup(BuiltInCardLookupKind Kind, BuiltInCard? Card);
