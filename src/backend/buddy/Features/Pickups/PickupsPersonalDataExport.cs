using buddy.Common.Erasure;
using buddy.Features.Babysitters;

namespace buddy.Features.Pickups;

// The "pickups" section: every pickup assignment on each child's schedule, past and future.
public sealed class PickupsPersonalDataExporter(IPickupScheduleEventStore schedules, IBabysitterListEventStore babysitters) : IPersonalDataExporter
{
    public Type Store => typeof(IPickupsStore);

    public string Section => "pickups";

    public async Task<object?> ExportAsync(ExportSubject subject, CancellationToken cancellationToken)
    {
        List<ExportedPickupSchedule> children = [];

        foreach (var childId in subject.Children)
        {
            var schedule = await schedules.FindIdForChildAsync(childId, cancellationToken) is { } id
                ? await schedules.FindSnapshotAsync(id, cancellationToken)
                : null;
            var assignments = schedule?.Assignments.OrderBy(a => a.Key.Date).ThenBy(a => a.Key.Slot).ToArray() ?? [];
            var names = await BabysitterNames.LoadAsync(assignments.Select(a => a.Value.Assignee), babysitters, cancellationToken);

            children.Add(new ExportedPickupSchedule(
                childId.Value,
                [.. assignments.Select(a => new ExportedPickup(
                    a.Key.Date,
                    a.Key.Slot,
                    PickupAssigneeDto.FromDomain(a.Value.Assignee, names),
                    a.Value.Time,
                    a.Value.Notes,
                    a.Value.AssignedBy.Value))]));
        }

        return children;
    }
}

public sealed record ExportedPickupSchedule(Guid ChildId, IReadOnlyList<ExportedPickup> Assignments);

public sealed record ExportedPickup(DateOnly Date, PickupSlot Slot, PickupAssigneeDto Assignee, TimeOnly? Time, string Notes, Guid AssignedBy);
