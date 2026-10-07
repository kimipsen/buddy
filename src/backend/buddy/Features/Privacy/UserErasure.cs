using buddy.Common.Erasure;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.Privacy;

// Erases a person across Buddy: the cascade policy of docs/backend/analysis/gdpr-data-protection.md
// (Questions 2 and 3), with each feature's IPersonalDataEraser doing its part.
//
//   1. Lock out: UserDeleted plus KeycloakIdentity.Deleted, in one transaction.
//   2. A guardian's children with no other guardian are erased too; the family data they anchor
//      passes to a sibling (the heir) when one remains.
//   3. Every feature's eraser, in Order.
//   4. The person: Keycloak account deleted, user stream masked, UserErased, ledger entry.
//
// Every step is idempotent and step 1 always runs first, so a failure anywhere leaves the user
// locked out and UserErasureService runs the whole erasure again until UserErased.
public sealed class UserErasure(
    IUserEventStore users,
    IGuardianLinkEventStore guardians,
    IKeycloakAdminClient keycloak,
    IEnumerable<IPersonalDataEraser> erasers,
    ILogger<UserErasure> logger)
{
    private readonly IPersonalDataEraser[] _erasers = [.. erasers.OrderBy(e => e.Order)];

    // DELETE /users/me: the lock-out must succeed (or the request fails); the rest is retried in
    // the background if it doesn't finish here.
    public async Task DeleteAccountAsync(User user, CancellationToken cancellationToken)
    {
        await LockAsync(user, cancellationToken);

        try
        {
            await EraseAsync(user.Id, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.ErasureIncomplete(exception, user.Id.Value);
        }
    }

    // Finishes every erasure that stopped halfway, and re-erases users a backup restore brought back.
    // UserErasureService calls it on a timer. Returns how many it finished.
    public async Task<int> FinishUnfinishedAsync(CancellationToken cancellationToken)
    {
        var unfinished = await users.ListUnfinishedErasuresAsync(cancellationToken);

        foreach (var userId in unfinished)
        {
            await EraseAsync(userId, cancellationToken);
        }

        return unfinished.Count;
    }

    // Erases a deleted (or ledger-listed) user, guardian or child. A no-op once erased.
    public async Task EraseAsync(UserId userId, CancellationToken cancellationToken)
    {
        if (await users.FindSnapshotAsync(userId, cancellationToken) is not { IsErased: false } user)
        {
            return;
        }

        await LockAsync(user, cancellationToken);

        if (await guardians.IsChildAsync(userId, cancellationToken))
        {
            await EraseChildAsync(user, await FindHeirAsync(userId, [userId], cancellationToken), cancellationToken);
        }
        else
        {
            await EraseGuardianAsync(user, cancellationToken);
        }
    }

    private async Task EraseGuardianAsync(User guardian, CancellationToken cancellationToken)
    {
        var orphans = new List<UserId>();

        foreach (var link in await guardians.ListForGuardianAsync(guardian.Id, cancellationToken))
        {
            var childId = new UserId(link.ChildId);
            var otherGuardians = (await guardians.ListForChildAsync(childId, cancellationToken))
                .Select(l => new UserId(l.GuardianId))
                .Where(id => id != guardian.Id);

            if (!await AnyActiveAsync(otherGuardians, cancellationToken))
            {
                orphans.Add(childId);
            }
        }

        // Heirs first, while every link is still in place: a sibling who is being erased in the
        // same run can't inherit.
        var heirs = new Dictionary<UserId, UserId?>();
        foreach (var orphan in orphans)
        {
            heirs[orphan] = await FindHeirAsync(orphan, orphans, cancellationToken);
        }

        foreach (var orphan in orphans)
        {
            if (await users.FindSnapshotAsync(orphan, cancellationToken) is { IsErased: false } child)
            {
                logger.OrphanedChildErased(orphan.Value, guardian.Id.Value);
                await LockAsync(child, cancellationToken);
                await EraseChildAsync(child, heirs[orphan], cancellationToken);
            }
        }

        var subject = new ErasureSubject(guardian.Id, EmailOf(guardian));

        foreach (var eraser in _erasers)
        {
            await eraser.EraseGuardianAsync(subject, cancellationToken);
        }

        await ErasePersonAsync(guardian, isChild: false, cancellationToken);
    }

    private async Task EraseChildAsync(User child, UserId? heir, CancellationToken cancellationToken)
    {
        if (heir is not null)
        {
            logger.FamilyDataPassedToHeir(child.Id.Value, heir.Value);
        }

        var subject = new ErasureSubject(child.Id, EmailOf(child));

        foreach (var eraser in _erasers)
        {
            await eraser.EraseChildAsync(subject, heir, cancellationToken);
        }

        await ErasePersonAsync(child, isChild: true, cancellationToken);
    }

    private async Task ErasePersonAsync(User user, bool isChild, CancellationToken cancellationToken)
    {
        // Before masking: masking erases the subject this call needs.
        if (user.KeycloakSubject.Value != Erased.Text)
        {
            await keycloak.DeleteUserAsync(user.KeycloakSubject, cancellationToken);
        }

        await users.EraseAsync(user.Id, cancellationToken);
        await users.RecordErasureAsync(user.Id, cancellationToken);

        logger.UserErased(user.Id.Value, isChild);
    }

    // UserDeleted (unless already there) and the identity's Deleted flag. A user deleted before the
    // flag existed gets it here, the first time UserErasureService picks them up.
    private Task LockAsync(User user, CancellationToken cancellationToken) =>
        user.KeycloakSubject.Value == Erased.Text
            ? Task.CompletedTask
            : users.DeleteAsync(
                user.Id,
                user.KeycloakSubject,
                user.IsDeleted ? [] : [new UserDeleted(user.Id, DateTimeOffset.UtcNow)],
                cancellationToken);

    // A sibling under the same (active) guardians who isn't being erased and isn't deleted. The same
    // family MealFamilyResolution sees; ordered so the choice is stable.
    private async Task<UserId?> FindHeirAsync(UserId childId, IReadOnlyCollection<UserId> beingErased, CancellationToken cancellationToken)
    {
        var siblings = new HashSet<UserId>();

        foreach (var guardianLink in await guardians.ListForChildAsync(childId, cancellationToken))
        {
            foreach (var siblingLink in await guardians.ListForGuardianAsync(new UserId(guardianLink.GuardianId), cancellationToken))
            {
                siblings.Add(new UserId(siblingLink.ChildId));
            }
        }

        foreach (var sibling in siblings.Where(s => !beingErased.Contains(s)).OrderBy(s => s.Value))
        {
            if (await users.FindSnapshotAsync(sibling, cancellationToken) is { IsDeleted: false })
            {
                return sibling;
            }
        }

        return null;
    }

    private async Task<bool> AnyActiveAsync(IEnumerable<UserId> userIds, CancellationToken cancellationToken)
    {
        foreach (var userId in userIds)
        {
            if (await users.FindSnapshotAsync(userId, cancellationToken) is { IsDeleted: false })
            {
                return true;
            }
        }

        return false;
    }

    private static string? EmailOf(User user) =>
        string.IsNullOrWhiteSpace(user.Email.Value) || user.Email.Value == Erased.Text ? null : user.Email.Value;
}
