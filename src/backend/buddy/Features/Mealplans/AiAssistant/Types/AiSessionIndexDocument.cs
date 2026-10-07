namespace buddy.Features.Mealplans;

// One row per session ever started (never removed) -- Id is the session's own Guid, so
// concurrent/repeated starts never collide. "The family's current session" is resolved by picking
// the row with the latest StartedAt among all of the family's children, not by looking up a single
// mutable pointer -- see AiSessionResolution.ResolveCurrentSessionIdAsync.
//
// LastActivityAt (the newest event's OccurredAt) and ContentErasedAt drive AiSessionRetention.
// Applying or discarding is always a session's last event, so LastActivityAt is also when a closed
// session closed. Both are null on rows written before retention existed; the sweep fills
// LastActivityAt from the stream the first time it looks at such a row.
public sealed record AiSessionIndexDocument(
    Guid Id,
    Guid ChildId,
    DateTimeOffset StartedAt,
    DateTimeOffset? LastActivityAt = null,
    DateTimeOffset? ContentErasedAt = null);
