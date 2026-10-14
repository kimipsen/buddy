using buddy.Common.Erasure;

namespace buddy.Features.Medicines;

// The "medicines" section: each child's medicine schedules with their full dose log, the group the
// child's medicines are shared with, and every effect note.
public sealed class MedicinesPersonalDataExporter(
    IMedicineEventStore medicines, IMedicineSharingEventStore sharing, IMedicineEffectLogEventStore effectLogs) : IPersonalDataExporter
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
            var effectLog = await effectLogs.FindSnapshotAsync(MedicineEffectLogId.ForChild(childId), cancellationToken);
            var schedulesById = schedules.ToDictionary(s => s.Id);

            children.Add(new ExportedChildMedicines(
                childId.Value,
                shared,
                [.. schedules.Select(s => new ExportedMedicineSchedule(
                    MedicineScheduleResponse.FromSchedule(s),
                    [.. s.DoseLog.OrderBy(d => d.Key.Date).ThenBy(d => d.Key.Time).Select(d => new ExportedDose(d.Key.Date, d.Key.Time, d.Value))]))],
                [.. (effectLog?.Notes.Values ?? []).OrderBy(n => n.Date).Select(n => MedicineEffectNoteResponse.From(n, schedulesById))]));
        }

        return children;
    }
}

public sealed record ExportedChildMedicines(
    Guid ChildId,
    Guid? SharedWithGroupId,
    IReadOnlyList<ExportedMedicineSchedule> Schedules,
    IReadOnlyList<MedicineEffectNoteResponse> EffectNotes);

public sealed record ExportedMedicineSchedule(MedicineScheduleResponse Schedule, IReadOnlyList<ExportedDose> Doses);

public sealed record ExportedDose(DateOnly Date, TimeOnly Time, DoseStatus Status);
