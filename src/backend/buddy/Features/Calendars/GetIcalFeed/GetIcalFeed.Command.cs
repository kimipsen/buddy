namespace buddy.Features.Calendars;

// Unauthenticated by design -- calendar client apps subscribe to a plain URL, not an OAuth
// header. The token in the URL is the authentication.
public sealed record GetIcalFeed(CalendarId CalendarId, string Token);

public sealed record GetIcalFeedResult(string? IcsContent);
