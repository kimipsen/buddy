using buddy.Common;
using buddy.Features.Users;

namespace buddy.Features.Guardians;

// Lets a guardian see a child's *other* guardians (e.g. a co-parent) -- ListMyGuardians only
// answers "who are the caller's own guardians" (caller == the child), which is a different
// question from "who are this child's guardians, as one of them". Added for Pickups' "assign a
// guardian" picker (see docs/backend/analysis/pickup-schedules.md), but lives in Guardians since
// it's a guardian-relationship query, not a Pickups concern.
public static class ListChildGuardiansHandler
{
    public static async Task<Result<IReadOnlyCollection<GuardianSummary>>> Handle(
        ListChildGuardians query, IGuardianLinkEventStore guardianLinks, IUserEventStore users, CancellationToken cancellationToken)
    {
        if (query.CallerId is not { } callerId)
        {
            return new Result<IReadOnlyCollection<GuardianSummary>>.NotFound();
        }

        if (await guardianLinks.FindActiveLinkAsync(query.ChildId, callerId, cancellationToken) is null)
        {
            return new Result<IReadOnlyCollection<GuardianSummary>>.NotFound();
        }

        var links = await guardianLinks.ListForChildAsync(query.ChildId, cancellationToken);
        var summaries = new List<GuardianSummary>(links.Count);

        foreach (var link in links)
        {
            var guardianEvents = await users.ReadAsync(new UserId(link.GuardianId), cancellationToken);

            if (User.Rehydrate(guardianEvents) is { IsDeleted: false } guardian)
            {
                summaries.Add(new GuardianSummary(guardian.Id, guardian.Name, new GuardianLinkId(link.GuardianLinkId), link.Kind));
            }
        }

        return new Result<IReadOnlyCollection<GuardianSummary>>.Success(summaries);
    }
}
