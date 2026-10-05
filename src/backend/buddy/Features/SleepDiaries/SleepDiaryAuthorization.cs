using System.Diagnostics;

using buddy.Common;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.SleepDiaries;

// Narrower than every other per-child feature: the guardian is the only principal with any access.
// A sleep diary is a guardian's clinical observation of the child, often written for a doctor, so
// the child gets no tier at all, not even read-only (docs/backend/analysis/sleep-diary.md,
// Question 4).
public enum SleepDiaryAccessTier
{
    None,
    // An active guardian of the child: log/clear entries, edit hygiene notes, view, share.
    Manage
}

public enum SleepDiaryAccess
{
    Allowed,
    // No guardian link -- including the child themself. Collapsed like MedicineAccess.NotFound, so
    // nobody can distinguish "no such child" from "not your child."
    NotFound
}

public static class SleepDiaryAccessExtensions
{
    public static Result<T> ToDeniedResult<T>(this SleepDiaryAccess access) => access switch
    {
        SleepDiaryAccess.NotFound => new Result<T>.NotFound(),
        SleepDiaryAccess.Allowed => throw new UnreachableException("ToDeniedResult called with SleepDiaryAccess.Allowed."),
        _ => throw new UnreachableException($"Unrecognized SleepDiaryAccess value: {access}."),
    };
}

public static class SleepDiaryAuthorization
{
    public static async Task<SleepDiaryAccess> CheckManage(UserId childId, UserId callerId, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        var tier = await ResolveTier(childId, callerId, guardians, cancellationToken);

        return tier switch
        {
            SleepDiaryAccessTier.Manage => SleepDiaryAccess.Allowed,
            SleepDiaryAccessTier.None => SleepDiaryAccess.NotFound,
            _ => throw new UnreachableException($"Unrecognized SleepDiaryAccessTier value: {tier}."),
        };
    }

    private static async Task<SleepDiaryAccessTier> ResolveTier(UserId childId, UserId callerId, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        var link = await guardians.FindActiveLinkAsync(childId, callerId, cancellationToken);

        return link is not null ? SleepDiaryAccessTier.Manage : SleepDiaryAccessTier.None;
    }
}
