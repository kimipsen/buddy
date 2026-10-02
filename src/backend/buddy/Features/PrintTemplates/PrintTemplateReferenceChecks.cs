using buddy.Features.Calendars;
using buddy.Features.Groups;
using buddy.Features.Guardians;
using buddy.Features.Users;
using buddy.Features.WorkLocations;

namespace buddy.Features.PrintTemplates;

// Write-time checks that catch mistakes -- they never grant access. At print time every row is
// fetched through its own feature's endpoint, which authorizes whoever is printing (see
// docs/backend/analysis/week-plan-print-templates.md#authorization). Each check runs as the
// caller, the guardian saving the template, and only over references that are new in this save:
// a group template legitimately holds rows another member added (their private calendar, their
// child), and a reference that went stale (an archived work location) must not block every later
// save. Returns an error message, or null when every new reference is currently reachable.
internal sealed class PrintTemplateReferenceChecks(
    UserId callerId,
    IGuardianLinkEventStore guardians,
    IGroupEventStore groups,
    ICalendarEventStore calendars,
    IWorkLocationScheduleEventStore workLocations)
{
    public async Task<string?> CheckRowsAsync(
        IReadOnlyList<PrintTemplateRow> rows, IReadOnlyList<PrintTemplateRow> existing, CancellationToken cancellationToken)
    {
        var knownChildren = existing.Select(r => r.ChildId).OfType<UserId>().ToHashSet();
        var knownGroups = existing.Select(r => r.MealGroupId).OfType<GroupId>().ToHashSet();
        var knownCalendars = existing.SelectMany(r => r.CalendarIds ?? []).ToHashSet();
        var knownWorkLocations = existing
            .Where(r => r.Kind == PrintRowKind.WorkLocation)
            .Select(r => (r.GuardianId, r.WorkLocationId))
            .ToHashSet();

        foreach (var childId in rows.Select(r => r.ChildId).OfType<UserId>().Distinct().Where(id => !knownChildren.Contains(id)))
        {
            if (await guardians.FindActiveLinkAsync(childId, callerId, cancellationToken) is null)
            {
                return "childId must be a child you are an active guardian of.";
            }
        }

        foreach (var groupId in rows.Select(r => r.MealGroupId).OfType<GroupId>().Distinct().Where(id => !knownGroups.Contains(id)))
        {
            if (!await PrintTemplateAuthorization.IsGuardianMemberAsync(groupId, callerId, groups, guardians, cancellationToken))
            {
                return "mealGroupId must be a group you are a member of.";
            }
        }

        foreach (var calendarId in rows.SelectMany(r => r.CalendarIds ?? []).Distinct().Where(id => !knownCalendars.Contains(id)))
        {
            var calendar = await calendars.FindSnapshotAsync(calendarId, cancellationToken);

            if (await CalendarAuthorization.CheckView(calendar, callerId, groups, guardians, cancellationToken) != CalendarAccess.Allowed)
            {
                return "calendarIds must be calendars you can currently view.";
            }
        }

        var newWorkLocations = rows
            .Where(r => r.Kind == PrintRowKind.WorkLocation && !knownWorkLocations.Contains((r.GuardianId, r.WorkLocationId)))
            .DistinctBy(r => (r.GuardianId, r.WorkLocationId));

        foreach (var row in newWorkLocations)
        {
            if (await CheckGuardianAsync(row.GuardianId!, cancellationToken) is { } guardianError)
            {
                return guardianError;
            }

            if (row.WorkLocationId is { } locationId)
            {
                var schedule = await workLocations.FindSnapshotAsync(WorkLocationScheduleId.ForGuardian(row.GuardianId!), cancellationToken);

                if (schedule?.FindActiveLocation(locationId) is null)
                {
                    return "workLocationId must be an active work location of that guardian.";
                }
            }
        }

        return null;
    }

    public async Task<string?> CheckGuardianColorsAsync(
        IReadOnlyList<GuardianColor> colors, IReadOnlyList<GuardianColor> existing, CancellationToken cancellationToken)
    {
        var known = existing.Select(c => c.GuardianId).ToHashSet();

        foreach (var guardianId in colors.Select(c => c.GuardianId).Where(id => !known.Contains(id)))
        {
            if (await CheckGuardianAsync(guardianId, cancellationToken) is { } error)
            {
                return error;
            }
        }

        return null;
    }

    private async Task<string?> CheckGuardianAsync(UserId guardianId, CancellationToken cancellationToken) =>
        guardianId == callerId || await WorkLocationAuthorization.AreCoGuardiansAsync(callerId, guardianId, guardians, cancellationToken)
            ? null
            : "guardianId must be you or another guardian of one of your children.";
}
