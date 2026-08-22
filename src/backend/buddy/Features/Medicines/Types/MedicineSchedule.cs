using System.Collections.Immutable;

using buddy.Features.Calendars;
using buddy.Features.Users;

namespace buddy.Features.Medicines;

public sealed record MedicineSchedule(
    MedicineId Id,
    UserId ChildId,
    UserId CreatedBy,
    string Name,
    string Dosage,
    Icon Icon,
    Color Color,
    IReadOnlyList<TimeOnly> Times,
    DateOnly StartDate,
    DateOnly? EndDate,
    ImmutableDictionary<(DateOnly Date, TimeOnly Time), DoseStatus> DoseLog,
    UserId LastModifiedBy,
    bool IsStopped = false)
{
    public static MedicineSchedule? Rehydrate(IEnumerable<MedicineEvent> events)
    {
        MedicineSchedule? schedule = null;

        foreach (var @event in events)
        {
            schedule = @event switch
            {
                MedicineScheduleCreated created => new MedicineSchedule(
                    created.Id,
                    created.ChildId,
                    created.CreatedBy,
                    created.Name,
                    created.Dosage,
                    created.Icon,
                    created.Color,
                    created.Times,
                    created.StartDate,
                    created.EndDate,
                    ImmutableDictionary<(DateOnly, TimeOnly), DoseStatus>.Empty,
                    created.CreatedBy),
                MedicineDetailsUpdated updated => schedule! with
                {
                    Name = updated.After.Name,
                    Dosage = updated.After.Dosage,
                    Icon = updated.After.Icon,
                    Color = updated.After.Color,
                    LastModifiedBy = updated.ModifiedBy
                },
                MedicineScheduleRescheduled rescheduled => schedule! with
                {
                    Times = rescheduled.After.Times,
                    StartDate = rescheduled.After.StartDate,
                    EndDate = rescheduled.After.EndDate,
                    LastModifiedBy = rescheduled.ModifiedBy
                },
                MedicineScheduleStopped stopped => schedule! with { IsStopped = true, LastModifiedBy = stopped.ModifiedBy },
                // Sparse log: a (Date, Time) with no entry is implicitly Pending, so an undo
                // (After: Pending) removes the key rather than storing it explicitly -- keeps the
                // log's size proportional to guardian/child actions, not to elapsed calendar time.
                DoseStatusChanged changed => schedule! with
                {
                    DoseLog = changed.After == DoseStatus.Pending
                        ? schedule!.DoseLog.Remove((changed.Date, changed.Time))
                        : schedule!.DoseLog.SetItem((changed.Date, changed.Time), changed.After),
                    LastModifiedBy = changed.ModifiedBy
                },
                _ => schedule
            };
        }

        return schedule;
    }
}
