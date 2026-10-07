using buddy.Common.Erasure;
using buddy.Features.Users;

using Marten;

namespace buddy.Features.Calendars;

// Calendars are shared, so they are masked, never deleted with one person. An erased person loses
// their explicit calendar roles; an erased child's items are deleted and their titles (which may name
// the child) masked. Items a guardian created stay: they are the family's plan, and CreatedBy is a
// pseudonym once the user is erased. See gdpr-data-protection.md.
public sealed class CalendarsPersonalDataEraser(
    ICalendarsStore store,
    ICalendarEventStore calendars,
    ICalendarItemEventStore items) : IPersonalDataEraser
{
    public Type Store => typeof(ICalendarsStore);

    public static void ConfigureMasking(StoreOptions options)
    {
        options.Events.AddMaskingRuleForProtectedInformation<CalendarCreatedForGroup>(e => e with { Name = Erased.Text });
        options.Events.AddMaskingRuleForProtectedInformation<EventItemCreated>(e => e with { Title = Erased.Text });
        options.Events.AddMaskingRuleForProtectedInformation<TaskItemCreated>(e => e with { Title = Erased.Text });
        options.Events.AddMaskingRuleForProtectedInformation<TemplateTaskItemCreated>(e => e with { Title = Erased.Text });
        options.Events.AddMaskingRuleForProtectedInformation<ItemDetailsUpdated>(e => e with
        {
            Before = e.Before with { Title = Erased.Text },
            After = e.After with { Title = Erased.Text },
        });
    }

    public Task EraseGuardianAsync(ErasureSubject guardian, CancellationToken cancellationToken) =>
        RevokeMembershipsAsync(guardian.UserId, cancellationToken);

    public async Task EraseChildAsync(ErasureSubject child, UserId? heir, CancellationToken cancellationToken)
    {
        await RevokeMembershipsAsync(child.UserId, cancellationToken);

        foreach (var itemId in await ListItemsAssignedToAsync(child.UserId, cancellationToken))
        {
            var id = new CalendarItemId(itemId);

            if (await items.FindSnapshotAsync(id, cancellationToken) is { IsDeleted: false })
            {
                await items.AppendAsync(id, [new ItemDeleted(id, child.UserId, DateTimeOffset.UtcNow)], cancellationToken);
            }

            await store.MaskStreamAsync<CalendarItemSnapshot>(itemId, cancellationToken);
        }
    }

    private async Task RevokeMembershipsAsync(UserId userId, CancellationToken cancellationToken)
    {
        foreach (var membership in await calendars.ListForUserAsync(userId, cancellationToken))
        {
            var calendarId = new CalendarId(membership.CalendarId);
            await calendars.AppendAsync(calendarId, [new MemberRoleRevoked(calendarId, userId, userId, DateTimeOffset.UtcNow)], cancellationToken);
        }
    }

    // The assignee lives inside the item's schedule (CalendarItem.Schedule.AssignedTo), which a LINQ
    // query over the snapshot can't reach, so this reads the snapshot JSON directly.
    private async Task<IReadOnlyList<Guid>> ListItemsAssignedToAsync(UserId childId, CancellationToken cancellationToken)
    {
        // Marten creates tables on first use, and raw SQL doesn't trigger that.
        await store.Storage.Database.EnsureStorageExistsAsync(typeof(CalendarItemSnapshot), cancellationToken);
        var table = store.Options.FindOrResolveDocumentType(typeof(CalendarItemSnapshot)).TableName.QualifiedName;

        await using var session = store.QuerySession();
        return await session.QueryAsync<Guid>(
            $"select id from {table} where data -> 'CalendarItem' -> 'Schedule' ->> 'AssignedTo' = ?",
            cancellationToken,
            childId.Value.ToString());
    }
}
