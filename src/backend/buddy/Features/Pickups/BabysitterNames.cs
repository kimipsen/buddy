using buddy.Features.Babysitters;
using buddy.Features.Users;

namespace buddy.Features.Pickups;

// Resolves the names of the babysitters a set of assignments refers to, loading each owning
// guardian's BabysitterList snapshot once per request. Archived babysitters still resolve, and so
// does a deleted account's list (UserDeleted leaves it in place); only an id missing from its
// owner's list reads as "". See
// docs/backend/analysis/babysitters.md, Question 4.
public sealed class BabysitterNames
{
    public static readonly BabysitterNames None = new(new Dictionary<BabysitterId, string>());

    private readonly IReadOnlyDictionary<BabysitterId, string> names;

    private BabysitterNames(IReadOnlyDictionary<BabysitterId, string> names) => this.names = names;

    public string NameOf(PickupAssignee.Babysitter babysitter) => names.GetValueOrDefault(babysitter.BabysitterId, "");

    public static async Task<BabysitterNames> LoadAsync(
        IEnumerable<PickupAssignee> assignees, IBabysitterListEventStore babysitters, CancellationToken cancellationToken)
    {
        // A union boxes as itself, not as its case, so OfType<PickupAssignee.Babysitter>() would never match.
        var owners = new HashSet<UserId>();

        foreach (var assignee in assignees)
        {
            if (assignee is PickupAssignee.Babysitter babysitter)
            {
                owners.Add(babysitter.GuardianId);
            }
        }

        if (owners.Count == 0)
        {
            return None;
        }

        var names = new Dictionary<BabysitterId, string>();

        foreach (var owner in owners)
        {
            if (await babysitters.FindSnapshotAsync(BabysitterListId.ForGuardian(owner), cancellationToken) is not { } list)
            {
                continue;
            }

            foreach (var babysitter in list.Babysitters)
            {
                names[babysitter.Id] = babysitter.Name;
            }
        }

        return new BabysitterNames(names);
    }
}
