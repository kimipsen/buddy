using System.Diagnostics;

using buddy.Common;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.Mealplans;

// Unlike CalendarAccess, neither Meal nor MealPlan has Members/Group-derived roles on the
// family/child axis -- exactly two principals ever apply there (see
// docs/backend/analysis/mealplans.md#authorization). Unlike MedicineAccessTier, those two tiers
// are asymmetric in a different way: the child can view and rate but never write the plan or a
// meal's details, and a guardian can view and write everything except a Rating.
//
// View exists purely for the second, additive axis -- a group a family has chosen to share its
// plan with (see docs/backend/analysis/group-owned-mealplans.md). The family/child resolution
// below (ResolveTier) never produces it -- only Rate or Manage. View and Manage are the two
// meaningful values a group's MealplanPermissionPolicy can hold; Rate is rejected there since
// it's the child's own tier, never a group member's.
public enum MealplanAccessTier
{
    None,
    // The child themself only: view meals/plan, rate a meal. Never a valid group-policy value.
    Rate,
    // An active guardian only: view meals/plan, create/edit/archive meals, assign/clear plan slots.
    Manage,
    // A group member whose MealplanPermissionPolicy entry is View: can see the shared plan and
    // meal library, but cannot create/edit/archive meals or assign/clear plan slots.
    View
}

public enum MealplanAccess
{
    Allowed,
    // No relationship to the child at all -- collapsed the same way MedicineAccess.NotFound is.
    NotFound,
    // The caller has some access but not the tier the action needs.
    Forbidden
}

public static class MealplanAccessExtensions
{
    public static Result<T> ToDeniedResult<T>(this MealplanAccess access) => access switch
    {
        MealplanAccess.Forbidden => new Result<T>.Forbidden(),
        MealplanAccess.NotFound => new Result<T>.NotFound(),
        MealplanAccess.Allowed => throw new UnreachableException("ToDeniedResult called with MealplanAccess.Allowed."),
        _ => throw new UnreachableException($"Unrecognized MealplanAccess value: {access}."),
    };
}

public static class MealplanAuthorization
{
    public static async Task<MealplanAccess> CheckView(UserId childId, UserId callerId, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        var tier = await ResolveTier(childId, callerId, guardians, cancellationToken);

        return tier == MealplanAccessTier.None ? MealplanAccess.NotFound : MealplanAccess.Allowed;
    }

    public static async Task<MealplanAccess> CheckManage(UserId childId, UserId callerId, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        var tier = await ResolveTier(childId, callerId, guardians, cancellationToken);

        return tier switch
        {
            MealplanAccessTier.Manage => MealplanAccess.Allowed,
            MealplanAccessTier.Rate => MealplanAccess.Forbidden,
            MealplanAccessTier.None => MealplanAccess.NotFound,
            _ => throw new UnreachableException($"Unrecognized MealplanAccessTier value: {tier}."),
        };
    }

    public static async Task<MealplanAccess> CheckRate(UserId childId, UserId callerId, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        var tier = await ResolveTier(childId, callerId, guardians, cancellationToken);

        return tier switch
        {
            MealplanAccessTier.Rate => MealplanAccess.Allowed,
            MealplanAccessTier.Manage => MealplanAccess.Forbidden,
            MealplanAccessTier.None => MealplanAccess.NotFound,
            _ => throw new UnreachableException($"Unrecognized MealplanAccessTier value: {tier}."),
        };
    }

    private static async Task<MealplanAccessTier> ResolveTier(UserId childId, UserId callerId, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        if (callerId == childId)
        {
            return MealplanAccessTier.Rate;
        }

        var link = await guardians.FindActiveLinkAsync(childId, callerId, cancellationToken);

        return link is not null ? MealplanAccessTier.Manage : MealplanAccessTier.None;
    }
}
