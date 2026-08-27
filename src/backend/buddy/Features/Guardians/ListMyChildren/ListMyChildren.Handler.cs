using buddy.Features.Calendars;
using buddy.Features.Users;

namespace buddy.Features.Guardians;

public static class ListMyChildrenHandler
{
    public static async Task<IReadOnlyCollection<ChildSummary>> Handle(
        ListMyChildren query, IGuardianLinkEventStore guardianLinks, IUserEventStore users, CancellationToken cancellationToken)
    {
        if (query.GuardianId is not { } guardianId)
        {
            return [];
        }

        var links = await guardianLinks.ListForGuardianAsync(guardianId, cancellationToken);
        var summaries = new List<ChildSummary>(links.Count);

        foreach (var link in links)
        {
            var childEvents = await users.ReadAsync(new UserId(link.ChildId), cancellationToken);

            if (User.Rehydrate(childEvents) is { IsDeleted: false } child)
            {
                summaries.Add(new ChildSummary(child.Id, child.Name, new GuardianLinkId(link.GuardianLinkId), link.Kind, child.ResolvedLanguage, child.ResolvedTimeZoneId));
            }
        }

        return summaries;
    }
}

public sealed record ChildSummary(UserId Id, Name Name, GuardianLinkId GuardianLinkId, GuardianKind Kind, Language Language, TimeZoneId TimeZoneId);
