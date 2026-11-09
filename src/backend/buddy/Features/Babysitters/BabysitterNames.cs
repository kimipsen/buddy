using buddy.Features.Users;

namespace buddy.Features.Babysitters;

// Resolves the names of the babysitters that pickups or nights refer to, loading each owning
// guardian's BabysitterList snapshot once per request. A reference is (GuardianId, BabysitterId);
// callers pass the owning guardians and look names up by BabysitterId. Archived babysitters still
// resolve, and so does a deleted account's list (UserDeleted leaves it in place); only an id
// missing from its owner's list reads as "". See docs/backend/analysis/babysitters.md, Question 4,
// and docs/backend/analysis/night-duty.md, "Refactors".
public sealed class BabysitterNames
{
    public static readonly BabysitterNames None = new(new Dictionary<BabysitterId, string>());

    private readonly IReadOnlyDictionary<BabysitterId, string> _names;

    private BabysitterNames(IReadOnlyDictionary<BabysitterId, string> names) => _names = names;

    public string NameOf(BabysitterId babysitterId) => _names.GetValueOrDefault(babysitterId, "");

    public static async Task<BabysitterNames> LoadAsync(
        IEnumerable<UserId> owners, IBabysitterListEventStore babysitters, CancellationToken cancellationToken)
    {
        var distinctOwners = owners.ToHashSet();

        if (distinctOwners.Count == 0)
        {
            return None;
        }

        var names = new Dictionary<BabysitterId, string>();

        foreach (var owner in distinctOwners)
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
