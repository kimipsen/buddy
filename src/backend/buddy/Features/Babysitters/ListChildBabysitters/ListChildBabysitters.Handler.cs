using buddy.Common;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.Babysitters;

public static class ListChildBabysittersHandler
{
    public static async Task<Result<IReadOnlyCollection<ChildBabysitter>>> Handle(
        ListChildBabysitters query,
        IBabysitterListEventStore store,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        var access = await BabysitterAuthorization.CheckPick(query.ChildId, query.UserId, guardians, cancellationToken);

        if (access != BabysitterAccess.Allowed)
        {
            return access.ToDeniedResult<IReadOnlyCollection<ChildBabysitter>>();
        }

        // Every current guardian's active babysitters -- a revoked guardian's drop out at once
        // (see docs/backend/analysis/babysitters.md, Question 5).
        var babysitters = new List<ChildBabysitter>();

        foreach (var link in await guardians.ListForChildAsync(query.ChildId, cancellationToken))
        {
            if (await store.FindSnapshotAsync(BabysitterListId.ForGuardian(new UserId(link.GuardianId)), cancellationToken) is not { } list)
            {
                continue;
            }

            babysitters.AddRange(list.ActiveBabysitters.Select(b => new ChildBabysitter(link.GuardianId, b.Id.Value, b.Name, b.ContactInfo)));
        }

        // Ordinal, so the order doesn't depend on the server's culture.
        babysitters.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        return new Result<IReadOnlyCollection<ChildBabysitter>>.Success(babysitters);
    }
}

// One entry of the pickup planner's babysitter dropdown. GuardianId is whose list it is on -- the
// pair goes into PickupAssignee.Babysitter as is.
public sealed record ChildBabysitter(Guid GuardianId, Guid Id, string Name, string ContactInfo);
