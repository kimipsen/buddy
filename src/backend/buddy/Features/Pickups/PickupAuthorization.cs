using System.Diagnostics;

using buddy.Common;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.Pickups;

// Same narrower two-tier shape MedicineSchedule and MealPlan both use -- no members, no group
// ownership, exactly two principals (see docs/backend/analysis/pickup-schedules.md#authorization).
// Unlike MedicineSchedule's Mark tier, the child has no write action at all -- there's nothing here
// for a child to self-report, so View is read-only by construction, not a collapsed write check.
public enum PickupAccessTier
{
    None,
    // The child themself: view the schedule only.
    View,
    // An active guardian only: everything View can do, plus assign/clear any slot.
    Manage
}

public enum PickupAccess
{
    Allowed,
    // No relationship to the child at all -- collapsed the same way MedicineAccess.NotFound is.
    NotFound,
    // The caller can View but the action needs Manage (e.g. the child tries to assign a slot).
    Forbidden
}

public static class PickupAccessExtensions
{
    public static Result<T> ToDeniedResult<T>(this PickupAccess access) => access switch
    {
        PickupAccess.Forbidden => new Result<T>.Forbidden(),
        PickupAccess.NotFound => new Result<T>.NotFound(),
        PickupAccess.Allowed => throw new UnreachableException("ToDeniedResult called with PickupAccess.Allowed."),
        _ => throw new UnreachableException($"Unrecognized PickupAccess value: {access}."),
    };
}

public static class PickupAuthorization
{
    public static async Task<PickupAccess> CheckView(UserId childId, UserId callerId, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        var tier = await ResolveTier(childId, callerId, guardians, cancellationToken);

        return tier == PickupAccessTier.None ? PickupAccess.NotFound : PickupAccess.Allowed;
    }

    public static async Task<PickupAccess> CheckManage(UserId childId, UserId callerId, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        var tier = await ResolveTier(childId, callerId, guardians, cancellationToken);

        return tier switch
        {
            PickupAccessTier.Manage => PickupAccess.Allowed,
            PickupAccessTier.View => PickupAccess.Forbidden,
            PickupAccessTier.None => PickupAccess.NotFound,
            _ => throw new UnreachableException($"Unrecognized PickupAccessTier value: {tier}."),
        };
    }

    private static async Task<PickupAccessTier> ResolveTier(UserId childId, UserId callerId, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        if (callerId == childId)
        {
            return PickupAccessTier.View;
        }

        var link = await guardians.FindActiveLinkAsync(childId, callerId, cancellationToken);

        return link is not null ? PickupAccessTier.Manage : PickupAccessTier.None;
    }
}
