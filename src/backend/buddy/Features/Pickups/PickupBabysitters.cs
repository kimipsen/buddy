using buddy.Features.Users;

namespace buddy.Features.Pickups;

internal static class PickupBabysitters
{
    // The guardians whose babysitter lists a set of assignees refers to, for BabysitterNames.LoadAsync.
    // A union boxes as itself, not as its case, so OfType<PickupAssignee.Babysitter>() would never match.
    public static IEnumerable<UserId> BabysitterOwners(this IEnumerable<PickupAssignee> assignees)
    {
        foreach (var assignee in assignees)
        {
            if (assignee is PickupAssignee.Babysitter babysitter)
            {
                yield return babysitter.GuardianId;
            }
        }
    }
}
