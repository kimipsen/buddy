namespace buddy.Features.Calendars;

// An IANA identifier (e.g. "Europe/Copenhagen") -- .NET's TimeZoneInfo resolves these on all
// platforms this backend targets. Validity is checked in handlers via TimeZoneResolution.IsValid,
// not here, to keep this type a plain data holder like the other value types.
public sealed record TimeZoneId(string Value)
{
    public static TimeZoneId New(string value) => new(value);
}
