using buddy.Common.Erasure;

namespace buddy.Features.Medicines;

// The "medicines" section: each child's medicine schedules with their full dose log, and the group
// the child's medicines are shared with.
public sealed class MedicinesPersonalDataExporter(IMedicineEventStore medicines, IMedicineSharingEventStore sharing) : IPersonalDataExporter
{
    public Type Store => typeof(IMedicinesStore);

    public string Section => "medicines";

    public async Task<object?> ExportAsync(ExportSubject subject, CancellationToken cancellationToken)
    {
        List<ExportedChildMedicines> children = [];

        foreach (var childId in subject.Children)
        {
            var schedules = await ListMedicineSchedulesHandler.ListForChildAsync(childId, medicines, cancellationToken);
            var shared = await sharing.FindIdForChildAsync(childId, cancellationToken) is { } sharingId
                ? (await sharing.FindSnapshotAsync(sharingId, cancellationToken))?.SharedWithGroupId?.Value
                : null;

            children.Add(new ExportedChildMedicines(
                childId.Value,
                shared,
                [.. schedules.Select(s => new ExportedMedicineSchedule(
                    MedicineScheduleResponse.FromSchedule(s),
                    [.. s.DoseLog.OrderBy(d => d.Key.Date).ThenBy(d => d.Key.Time).Select(d => new ExportedDose(d.Key.Date, d.Key.Time, d.Value))]))]));
        }

        return children;
    }
}

public sealed record ExportedChildMedicines(Guid ChildId, Guid? SharedWithGroupId, IReadOnlyList<ExportedMedicineSchedule> Schedules);

public sealed record ExportedMedicineSchedule(MedicineScheduleResponse Schedule, IReadOnlyList<ExportedDose> Doses);

public sealed record ExportedDose(DateOnly Date, TimeOnly Time, DoseStatus Status);
