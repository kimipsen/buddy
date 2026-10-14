using buddy.Features.Users;

namespace buddy.Features.Guardians;

public static class ListMyGuardiansHandler
{
    public static async Task<IReadOnlyCollection<GuardianSummary>> Handle(
        ListMyGuardians query, IGuardianLinkEventStore guardianLinks, IUserEventStore users, CancellationToken cancellationToken)
    {
        var childId = query.ChildId;

        var links = await guardianLinks.ListForChildAsync(childId, cancellationToken);
        var summaries = new List<GuardianSummary>(links.Count);

        foreach (var link in links)
        {
            var guardianEvents = await users.ReadAsync(new UserId(link.GuardianId), cancellationToken);

            if (User.Rehydrate(guardianEvents) is { IsDeleted: false } guardian)
            {
                summaries.Add(new GuardianSummary(guardian.Id, guardian.Name, guardian.CallingName, new GuardianLinkId(link.GuardianLinkId), link.Kind));
            }
        }

        return summaries;
    }
}

// CallingName is what the child calls this guardian ("Mor"), "" when the guardian hasn't set one.
public sealed record GuardianSummary(UserId Id, Name Name, string CallingName, GuardianLinkId GuardianLinkId, GuardianKind Kind);
