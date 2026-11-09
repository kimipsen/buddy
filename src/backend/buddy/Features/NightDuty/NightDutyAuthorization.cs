using System.Diagnostics;

using buddy.Common;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.NightDuty;

// The Pickups tiers (docs/backend/analysis/night-duty.md, Question 6): an active guardian of the
// child Manages, the child themself Views resolved nights, everyone else gets NotFound. Copied
// rather than shared with PickupAuthorization so the two features stay independent, the same
// trade-off WorkLocationAuthorization made.
public enum NightDutyAccessTier
{
    None,
    // The child themself: read resolved nights only.
    View,
    // An active guardian: everything.
    Manage
}

public enum NightDutyAccess
{
    Allowed,
    // No relationship to the child at all.
    NotFound,
    // The caller can View but the action needs Manage (the child calling a write or the editor read).
    Forbidden
}

public static class NightDutyAccessExtensions
{
    public static Result<T> ToDeniedResult<T>(this NightDutyAccess access) => access switch
    {
        NightDutyAccess.Forbidden => new Result<T>.Forbidden(),
        NightDutyAccess.NotFound => new Result<T>.NotFound(),
        NightDutyAccess.Allowed => throw new UnreachableException("ToDeniedResult called with NightDutyAccess.Allowed."),
        _ => throw new UnreachableException($"Unrecognized NightDutyAccess value: {access}."),
    };
}

public static class NightDutyAuthorization
{
    public static async Task<NightDutyAccess> CheckView(UserId childId, UserId callerId, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        var tier = await ResolveTier(childId, callerId, guardians, cancellationToken);

        return tier == NightDutyAccessTier.None ? NightDutyAccess.NotFound : NightDutyAccess.Allowed;
    }

    public static async Task<NightDutyAccess> CheckManage(UserId childId, UserId callerId, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        var tier = await ResolveTier(childId, callerId, guardians, cancellationToken);

        return tier switch
        {
            NightDutyAccessTier.Manage => NightDutyAccess.Allowed,
            NightDutyAccessTier.View => NightDutyAccess.Forbidden,
            NightDutyAccessTier.None => NightDutyAccess.NotFound,
            _ => throw new UnreachableException($"Unrecognized NightDutyAccessTier value: {tier}."),
        };
    }

    private static async Task<NightDutyAccessTier> ResolveTier(UserId childId, UserId callerId, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        if (callerId == childId)
        {
            return NightDutyAccessTier.View;
        }

        var link = await guardians.FindActiveLinkAsync(childId, callerId, cancellationToken);

        return link is not null ? NightDutyAccessTier.Manage : NightDutyAccessTier.None;
    }
}
