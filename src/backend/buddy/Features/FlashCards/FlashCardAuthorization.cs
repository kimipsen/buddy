using System.Diagnostics;

using buddy.Common;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.FlashCards;

public enum FlashCardAccess
{
    Allowed,
    // No relationship at all: the set or child is not revealed to the caller.
    NotFound,
    // A relationship exists but the action needs more (a child managing the library or a child's
    // practice setup).
    Forbidden
}

public static class FlashCardAccessExtensions
{
    public static Result<T> ToDeniedResult<T>(this FlashCardAccess access) => access switch
    {
        FlashCardAccess.Forbidden => new Result<T>.Forbidden(),
        FlashCardAccess.NotFound => new Result<T>.NotFound(),
        FlashCardAccess.Allowed => throw new UnreachableException("ToDeniedResult called with FlashCardAccess.Allowed."),
        _ => throw new UnreachableException($"Unrecognized FlashCardAccess value: {access}."),
    };
}

// The library is guardian-only: a guardian sees and edits their own sets and those of their
// co-guardians (guardians who share an active link to at least one child). A child never manages
// the library, only reads their own enabled sets (GetChildFlashCards) and plays them.
public static class FlashCardAuthorization
{
    // Any account that is not a child may manage the library.
    public static async Task<FlashCardAccess> CheckManageLibrary(UserId callerId, IGuardianLinkEventStore guardians, CancellationToken cancellationToken) =>
        await IsChild(callerId, guardians, cancellationToken) ? FlashCardAccess.Forbidden : FlashCardAccess.Allowed;

    // Manage access to one set: its owner or a current co-guardian of the owner. A missing set and
    // an unrelated caller are both NotFound. Deletion is not judged here: the handlers decide what a
    // deleted set allows.
    public static async Task<FlashCardAccess> CheckManageSet(FlashCardSet? set, UserId callerId, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        if (await IsChild(callerId, guardians, cancellationToken))
        {
            return FlashCardAccess.Forbidden;
        }

        if (set is null)
        {
            return FlashCardAccess.NotFound;
        }

        if (set.OwnerId == callerId)
        {
            return FlashCardAccess.Allowed;
        }

        var visibleOwners = await LibraryOwners(callerId, guardians, cancellationToken);

        return visibleOwners.Contains(set.OwnerId) ? FlashCardAccess.Allowed : FlashCardAccess.NotFound;
    }

    // The caller plus every guardian linked to one of the caller's children.
    public static async Task<IReadOnlyCollection<UserId>> LibraryOwners(UserId callerId, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        HashSet<UserId> owners = [callerId];

        foreach (var link in await guardians.ListForGuardianAsync(callerId, cancellationToken))
        {
            foreach (var coGuardian in await guardians.ListForChildAsync(new UserId(link.ChildId), cancellationToken))
            {
                owners.Add(new UserId(coGuardian.GuardianId));
            }
        }

        return owners;
    }

    // Reading a child's enabled sets: the child themself or any active guardian of the child.
    public static async Task<FlashCardAccess> CheckViewChild(UserId childId, UserId callerId, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        if (callerId == childId)
        {
            return FlashCardAccess.Allowed;
        }

        return await guardians.FindActiveLinkAsync(childId, callerId, cancellationToken) is null
            ? FlashCardAccess.NotFound
            : FlashCardAccess.Allowed;
    }

    // Configuring a child's practice: an active guardian only; the child sees it but cannot change it.
    public static async Task<FlashCardAccess> CheckManageChild(UserId childId, UserId callerId, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        if (callerId == childId)
        {
            return FlashCardAccess.Forbidden;
        }

        return await guardians.FindActiveLinkAsync(childId, callerId, cancellationToken) is null
            ? FlashCardAccess.NotFound
            : FlashCardAccess.Allowed;
    }

    // The play routes (/me/...) belong to a child: the child is the caller, never a request field. A
    // guardian has no practice stream of their own, so they get NotFound.
    public static async Task<FlashCardAccess> CheckPlay(UserId callerId, IGuardianLinkEventStore guardians, CancellationToken cancellationToken) =>
        await guardians.IsChildAsync(callerId, cancellationToken) ? FlashCardAccess.Allowed : FlashCardAccess.NotFound;

    private static async Task<bool> IsChild(UserId callerId, IGuardianLinkEventStore guardians, CancellationToken cancellationToken) =>
        (await guardians.ListForChildAsync(callerId, cancellationToken)).Count > 0;
}
