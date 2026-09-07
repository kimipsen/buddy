using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.Mealplans;

// Distinct from MealFamilyResolution's ResolveFamilyMealPlanIdAsync/ResolveFamilyAiCredentialIdAsync:
// those resolve a single stream that's written once and appended to forever, so "the first row
// found among the family" is always correct regardless of which sibling is asked. A session can be
// superseded by a later one anchored under a *different* sibling, so resolving "current" here
// means finding the most recently started session across every child in the family, not just the
// first row any of them happens to have.
public static class AiSessionResolution
{
    public static async Task<MealplanAiSessionId?> ResolveCurrentSessionIdAsync(
        UserId childId, IGuardianLinkEventStore guardians, IAiSessionEventStore sessions, CancellationToken cancellationToken)
    {
        var family = await MealFamilyResolution.ResolveFamilyAsync(childId, guardians, cancellationToken);

        (MealplanAiSessionId Id, DateTimeOffset StartedAt)? latest = null;

        foreach (var member in family)
        {
            var candidate = await sessions.FindLatestForChildAsync(member, cancellationToken);

            if (candidate is { } found && (latest is null || found.StartedAt > latest.Value.StartedAt))
            {
                latest = found;
            }
        }

        return latest?.Id;
    }
}
