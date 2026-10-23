namespace buddy.Features.Pickups;

// Feature-local, like Mealplans' IcalTokenId -- see docs/backend/analysis/pickup-ical-feed.md,
// Question 1.
public sealed record IcalTokenId(Guid Value)
{
    public static IcalTokenId New() => new(Guid.CreateVersion7());
}
