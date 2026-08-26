using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.Calendars;

// A child sees only tasks assigned to themself -- never an unassigned task, and never a sibling's.
// Events are never filtered (there's no assignee concept for them). "Child" is resolved the same
// way AccountService does on the frontend: having at least one active guardian link, since a
// guardian account never itself has a guardian in this single-realm model (see
// docs/backend/analysis/child-accounts-and-guardian-roles.md). A guardian's own view is untouched.
public static class ChildVisibility
{
    public static async Task<bool> IsChildAsync(UserId userId, IGuardianLinkEventStore guardians, CancellationToken cancellationToken) =>
        (await guardians.ListForChildAsync(userId, cancellationToken)).Count > 0;

    public static IReadOnlyCollection<CalendarItem> FilterForChild(IReadOnlyCollection<CalendarItem> items, UserId childId) =>
        [.. items.Where(item => item.Kind == CalendarItemKind.Event || item.AssignedTo == childId)];

    public static IReadOnlyCollection<CalendarItemOccurrence> FilterForChild(IReadOnlyCollection<CalendarItemOccurrence> occurrences, UserId childId) =>
        [.. occurrences.Where(occurrence => occurrence.Kind == CalendarItemKind.Event || occurrence.AssignedTo == childId.Value)];
}
