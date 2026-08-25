using buddy.Features.Users;

namespace buddy.Features.Guardians;

// Answers "who are this child's siblings" (other children sharing at least one of the caller's own
// active guardians) -- distinct from ListMyChildren (caller is the guardian) and ListMyGuardians
// (caller is the child, but the question is about guardians, not co-children). Needed so the child
// home screen can show a sibling's name for a pickup/drop-off assignment instead of just "a sibling"
// (see docs/frontend/analysis -- pickup occurrences only carry SiblingChildId, never a name).
public static class ListMySiblingsHandler
{
    public static async Task<IReadOnlyCollection<SiblingSummary>> Handle(
        ListMySiblings query, IGuardianLinkEventStore guardianLinks, IUserEventStore users, CancellationToken cancellationToken)
    {
        if (query.ChildId is not { } childId)
        {
            return [];
        }

        var myGuardianLinks = await guardianLinks.ListForChildAsync(childId, cancellationToken);
        var siblingIds = new HashSet<Guid>();

        foreach (var link in myGuardianLinks)
        {
            var guardianChildren = await guardianLinks.ListForGuardianAsync(new UserId(link.GuardianId), cancellationToken);

            foreach (var siblingLink in guardianChildren)
            {
                if (siblingLink.ChildId != childId.Value)
                {
                    siblingIds.Add(siblingLink.ChildId);
                }
            }
        }

        var summaries = new List<SiblingSummary>(siblingIds.Count);

        foreach (var siblingId in siblingIds)
        {
            var siblingEvents = await users.ReadAsync(new UserId(siblingId), cancellationToken);

            if (User.Rehydrate(siblingEvents) is { IsDeleted: false } sibling)
            {
                summaries.Add(new SiblingSummary(sibling.Id, sibling.Name));
            }
        }

        return summaries;
    }
}

public sealed record SiblingSummary(UserId Id, Name Name);
