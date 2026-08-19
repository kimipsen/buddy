namespace buddy.Features.Calendars;

// Local wall-clock date and time -- no offset. The owning Calendar's TimeZoneId resolves this to
// an actual instant, which lets recurring events keep the same local time across a DST boundary
// instead of drifting by an hour.
public sealed record StartsAt(DateOnly Date, TimeOnly Time);
