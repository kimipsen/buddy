using buddy.Features.Calendars;
using buddy.Features.Users;

namespace buddy.Features.Medicines;

public union MedicineEvent(
    MedicineScheduleCreated,
    MedicineDetailsUpdated,
    MedicineScheduleRescheduled,
    MedicineScheduleStopped,
    DoseStatusChanged,
    MedicineEffectInclusionChanged
)
{
    public static MedicineEvent FromPayload(object payload) => payload switch
    {
        MedicineScheduleCreated e => e,
        MedicineDetailsUpdated e => e,
        MedicineScheduleRescheduled e => e,
        MedicineScheduleStopped e => e,
        DoseStatusChanged e => e,
        MedicineEffectInclusionChanged e => e,
        _ => throw new ArgumentException($"Unknown medicine event payload: {payload.GetType().Name}", nameof(payload)),
    };

    public string EventType => this switch
    {
        MedicineScheduleCreated => nameof(MedicineScheduleCreated),
        MedicineDetailsUpdated => nameof(MedicineDetailsUpdated),
        MedicineScheduleRescheduled => nameof(MedicineScheduleRescheduled),
        MedicineScheduleStopped => nameof(MedicineScheduleStopped),
        DoseStatusChanged => nameof(DoseStatusChanged),
        MedicineEffectInclusionChanged => nameof(MedicineEffectInclusionChanged),
    };
}

public sealed record MedicineScheduleCreated(
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
    DateTimeOffset OccurredAt);

public sealed record MedicineDetailsUpdated(MedicineId Id, MedicineDetails Before, MedicineDetails After, UserId ModifiedBy, DateTimeOffset OccurredAt);

public sealed record MedicineScheduleRescheduled(MedicineId Id, MedicineWindow Before, MedicineWindow After, UserId ModifiedBy, DateTimeOffset OccurredAt);

public sealed record MedicineScheduleStopped(MedicineId Id, UserId ModifiedBy, DateTimeOffset OccurredAt);

// Also used to undo a mark -- After: DoseStatus.Pending, no separate "unmark" event, the same way
// RecurrenceUpdated covers both adding and removing a recurrence via After: Recurrence.OneOff.
public sealed record DoseStatusChanged(MedicineId Id, DateOnly Date, TimeOnly Time, DoseStatus Before, DoseStatus After, UserId ModifiedBy, DateTimeOffset OccurredAt);

// Before/After are IsExcludedFromEffects: true leaves the medicine out of the effect summaries
// (docs/backend/analysis/medicine-effect-inclusion.md). Allowed on a stopped schedule too.
public sealed record MedicineEffectInclusionChanged(MedicineId Id, bool Before, bool After, UserId ModifiedBy, DateTimeOffset OccurredAt);
