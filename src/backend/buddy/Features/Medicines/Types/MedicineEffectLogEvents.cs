using buddy.Features.Users;

namespace buddy.Features.Medicines;

public union MedicineEffectLogEvent(
    MedicineEffectLogStarted,
    MedicineEffectNoteLogged,
    MedicineEffectNoteCleared
)
{
    public static MedicineEffectLogEvent FromPayload(object payload) => payload switch
    {
        MedicineEffectLogStarted e => e,
        MedicineEffectNoteLogged e => e,
        MedicineEffectNoteCleared e => e,
        _ => throw new ArgumentException($"Unknown medicine effect log event payload: {payload.GetType().Name}", nameof(payload)),
    };

    public string EventType => this switch
    {
        MedicineEffectLogStarted => nameof(MedicineEffectLogStarted),
        MedicineEffectNoteLogged => nameof(MedicineEffectNoteLogged),
        MedicineEffectNoteCleared => nameof(MedicineEffectNoteCleared),
    };
}

// Appended lazily by the first LogMedicineEffectNote for a child, in the same CreateAsync, as
// SleepDiaryStarted.
public sealed record MedicineEffectLogStarted(MedicineEffectLogId Id, UserId ChildId, DateTimeOffset OccurredAt);

// Always overwrites the whole day. LoggedBy is After.LoggedBy.
public sealed record MedicineEffectNoteLogged(MedicineEffectLogId Id, DateOnly Date, MedicineEffectNote? Before, MedicineEffectNote After, DateTimeOffset OccurredAt);

public sealed record MedicineEffectNoteCleared(MedicineEffectLogId Id, DateOnly Date, MedicineEffectNote Before, UserId ModifiedBy, DateTimeOffset OccurredAt);
