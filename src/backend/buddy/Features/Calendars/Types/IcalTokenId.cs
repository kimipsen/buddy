namespace buddy.Features.Calendars;

public sealed record IcalTokenId(Guid Value)
{
    public static IcalTokenId New() => new(Guid.CreateVersion7());
}
