using buddy.Features.Calendars;
using buddy.Features.Users;

namespace buddy.Features.Guardians;

public static class ListMyChildrenHandler
{
    public static async Task<IReadOnlyCollection<ChildSummary>> Handle(
        ListMyChildren query, IGuardianLinkEventStore guardianLinks, IUserEventStore users, CancellationToken cancellationToken)
    {
        var guardianId = query.GuardianId;

        var links = await guardianLinks.ListForGuardianAsync(guardianId, cancellationToken);
        var summaries = new List<ChildSummary>(links.Count);

        foreach (var link in links)
        {
            var childEvents = await users.ReadAsync(new UserId(link.ChildId), cancellationToken);

            if (User.Rehydrate(childEvents) is { IsDeleted: false } child)
            {
                summaries.Add(ChildSummary.From(child, link));
            }
        }

        return summaries;
    }
}

public sealed record ChildSummary(
    UserId Id,
    Name Name,
    GuardianLinkId GuardianLinkId,
    GuardianKind Kind,
    Language Language,
    TimeZoneId TimeZoneId,
    bool ShowVisualTimers,
    HomeworkChunks HomeworkChunks,
    TimerPreferences TimerPreferences,
    bool AllowChime)
{
    // Every Guardians route that returns a child builds it from the child's User and the caller's
    // active GuardianLink.
    public static ChildSummary From(User child, GuardianLinkDocument link) => new(
        child.Id,
        child.Name,
        new GuardianLinkId(link.GuardianLinkId),
        link.Kind,
        child.Language,
        child.TimeZoneId,
        child.ShowVisualTimers,
        child.HomeworkChunks,
        child.TimerPreferences,
        child.AllowChime);
}
