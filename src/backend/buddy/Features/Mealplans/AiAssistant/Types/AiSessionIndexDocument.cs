namespace buddy.Features.Mealplans;

// One row per session ever started (never updated or removed) -- Id is the session's own Guid, so
// concurrent/repeated starts never collide. "The family's current session" is resolved by picking
// the row with the latest StartedAt among all of the family's children, not by looking up a single
// mutable pointer -- see AiSessionResolution.ResolveCurrentSessionIdAsync.
public sealed record AiSessionIndexDocument(Guid Id, Guid ChildId, DateTimeOffset StartedAt);
