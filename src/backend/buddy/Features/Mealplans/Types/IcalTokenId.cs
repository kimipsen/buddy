namespace buddy.Features.Mealplans;

public sealed record IcalTokenId(Guid Value)
{
    public static IcalTokenId New() => new(Guid.CreateVersion7());
}
