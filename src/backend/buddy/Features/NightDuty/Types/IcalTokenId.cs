namespace buddy.Features.NightDuty;

// Feature-local, like Pickups' and Mealplans' IcalTokenId -- see
// docs/backend/analysis/night-duty-ical-feed.md, Question 1.
public sealed record IcalTokenId(Guid Value)
{
    public static IcalTokenId New() => new(Guid.CreateVersion7());
}
