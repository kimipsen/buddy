namespace buddy.Common;

// Shared vocabulary for expected, recoverable command/query outcomes. NotFound/Forbidden mirror
// the access checks already used across Calendars/Groups; Validation carries a business-rule
// message. A handler with no success payload uses Result<Unit>. Outcomes that don't fit this
// shape -- CreateCalendar/CreateGroup's Unauthenticated case, ResendEmailVerification's cooldown
// -- stay as their own feature-specific type rather than stretching this one to cover every case:
// every switch over Result<T> must handle all four cases, so folding in a case only one or two
// features ever produce would force unreachable arms everywhere else.
public union Result<T>(Result<T>.Success, Result<T>.NotFound, Result<T>.Forbidden, Result<T>.Validation)
{
    public sealed record Success(T Value);
    public sealed record NotFound;
    public sealed record Forbidden;
    public sealed record Validation(string Message);
}
