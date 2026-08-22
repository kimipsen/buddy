using System.Diagnostics;

using buddy.Common;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.Medicines;

// Unlike CalendarAccess, a MedicineSchedule has no Members/Group-derived roles -- exactly two
// principals ever apply (see docs/backend/analysis/medicine-schedules.md#authorization), so the
// tiers below replace CalendarRole's Owner/Contributor/Viewer spread.
public enum MedicineAccessTier
{
    None,
    // The child themself, or a guardian: view today's doses, mark/unmark a dose.
    Mark,
    // An active guardian only: everything Mark can do, plus create/edit/stop the schedule itself.
    Manage
}

public enum MedicineAccess
{
    Allowed,
    // No relationship to the child at all -- collapsed the same way CalendarAccess.NotFound is,
    // so a stranger can't distinguish "no such child" from "not your child."
    NotFound,
    // The caller can Mark but the action needs Manage (e.g. the child tries to edit the schedule).
    Forbidden
}

public static class MedicineAccessExtensions
{
    public static Result<T> ToDeniedResult<T>(this MedicineAccess access) => access switch
    {
        MedicineAccess.Forbidden => new Result<T>.Forbidden(),
        MedicineAccess.NotFound => new Result<T>.NotFound(),
        MedicineAccess.Allowed => throw new UnreachableException("ToDeniedResult called with MedicineAccess.Allowed."),
        _ => throw new UnreachableException($"Unrecognized MedicineAccess value: {access}."),
    };
}

public static class MedicineAuthorization
{
    public static async Task<MedicineAccess> CheckMark(UserId childId, UserId callerId, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        var tier = await ResolveTier(childId, callerId, guardians, cancellationToken);

        return tier == MedicineAccessTier.None ? MedicineAccess.NotFound : MedicineAccess.Allowed;
    }

    public static async Task<MedicineAccess> CheckManage(UserId childId, UserId callerId, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        var tier = await ResolveTier(childId, callerId, guardians, cancellationToken);

        return tier switch
        {
            MedicineAccessTier.Manage => MedicineAccess.Allowed,
            MedicineAccessTier.Mark => MedicineAccess.Forbidden,
            MedicineAccessTier.None => MedicineAccess.NotFound,
            _ => throw new UnreachableException($"Unrecognized MedicineAccessTier value: {tier}."),
        };
    }

    private static async Task<MedicineAccessTier> ResolveTier(UserId childId, UserId callerId, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        if (callerId == childId)
        {
            return MedicineAccessTier.Mark;
        }

        var link = await guardians.FindActiveLinkAsync(childId, callerId, cancellationToken);

        return link is not null ? MedicineAccessTier.Manage : MedicineAccessTier.None;
    }
}
