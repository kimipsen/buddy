using buddy.Features.Users;

namespace buddy.Features.Guardians;

public sealed record GuardianLink(
    GuardianLinkId Id,
    UserId ChildId,
    UserId GuardianId,
    GuardianKind Kind,
    bool IsRevoked = false)
{
    public static GuardianLink? Rehydrate(IEnumerable<GuardianEvent> events)
    {
        GuardianLink? link = null;

        foreach (var @event in events)
        {
            link = @event switch
            {
                GuardianLinked linked => new GuardianLink(linked.GuardianLinkId, linked.ChildId, linked.GuardianId, linked.Kind),
                GuardianKindChanged changed => link! with { Kind = changed.After },
                GuardianRevoked => link! with { IsRevoked = true },
                _ => link
            };
        }

        return link;
    }
}
