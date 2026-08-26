using buddy.Features.Calendars;
using buddy.Features.Users;

namespace buddy.Features.Progress;

// Internal command only -- no HTTP endpoint. Called explicitly by SetTaskCompletionHandler after
// its own TaskCompletionChanged append succeeds; see docs/backend/analysis/gamified-progress.md
// for why this is a synchronous cross-feature call rather than a reactive projection.
public sealed record RecordStarChange(UserId ChildId, CalendarItemId ItemId, DateOnly OccurrenceDate, bool IsCompleted);
