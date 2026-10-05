using buddy.Features.Users;

namespace buddy.Features.SleepDiaries;

public union SleepDiaryEvent(
    SleepDiaryStarted,
    SleepEntryLogged,
    SleepEntryCleared,
    SleepHygieneNotesUpdated
)
{
    public static SleepDiaryEvent FromPayload(object payload) => payload switch
    {
        SleepDiaryStarted e => e,
        SleepEntryLogged e => e,
        SleepEntryCleared e => e,
        SleepHygieneNotesUpdated e => e,
        _ => throw new ArgumentException($"Unknown sleep diary event payload: {payload.GetType().Name}", nameof(payload)),
    };

    public string EventType => this switch
    {
        SleepDiaryStarted => nameof(SleepDiaryStarted),
        SleepEntryLogged => nameof(SleepEntryLogged),
        SleepEntryCleared => nameof(SleepEntryCleared),
        SleepHygieneNotesUpdated => nameof(SleepHygieneNotesUpdated),
    };
}

// Appended lazily by the first LogSleepEntry/UpdateSleepHygieneNotes call for a child with no
// stream yet, in the same CreateAsync -- not provisioned at child creation, the same way
// PickupScheduleCreated/ProgressStarted are decoupled from CreateChild.
public sealed record SleepDiaryStarted(SleepDiaryId Id, UserId ChildId, DateTimeOffset OccurredAt);

// Always overwrites the whole day -- re-editing a logged night (adding detail the next morning) is
// the expected case, not a conflict. LoggedBy is After.LoggedBy.
public sealed record SleepEntryLogged(SleepDiaryId Id, DateOnly Date, SleepEntry? Before, SleepEntry After, DateTimeOffset OccurredAt);

public sealed record SleepEntryCleared(SleepDiaryId Id, DateOnly Date, SleepEntry Before, UserId ModifiedBy, DateTimeOffset OccurredAt);

// The diary-wide "sleep hygiene measures/routines/rituals" box. FreeText convention: "" means none.
public sealed record SleepHygieneNotesUpdated(SleepDiaryId Id, string Before, string After, UserId ModifiedBy, DateTimeOffset OccurredAt);
