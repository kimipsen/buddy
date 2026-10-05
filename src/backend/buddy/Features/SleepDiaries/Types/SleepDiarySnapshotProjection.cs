using Marten.Events.Aggregation;

namespace buddy.Features.SleepDiaries;

// Thin wrapper: Marten can't use the sealed-record SleepDiaryId as a document Id. See
// PickupScheduleSnapshot and docs/backend/analysis/event-stream-snapshots.md, Question 5.
public sealed record SleepDiarySnapshot(Guid Id, SleepDiary SleepDiary);

// Inline snapshot in the shared "snapshots" schema -- derived, rebuildable state, never a second
// source of truth.
public sealed class SleepDiarySnapshotProjection : SingleStreamProjection<SleepDiarySnapshot, Guid>
{
    public static SleepDiarySnapshot Create(SleepDiaryStarted started) =>
        new(started.Id.Value, SleepDiary.Start(SleepDiaryEvent.FromPayload(started)));

    public SleepDiarySnapshot Apply(SleepDiarySnapshot current, SleepEntryLogged logged) =>
        current with { SleepDiary = SleepDiary.Advance(current.SleepDiary, SleepDiaryEvent.FromPayload(logged)) };

    public SleepDiarySnapshot Apply(SleepDiarySnapshot current, SleepEntryCleared cleared) =>
        current with { SleepDiary = SleepDiary.Advance(current.SleepDiary, SleepDiaryEvent.FromPayload(cleared)) };

    public SleepDiarySnapshot Apply(SleepDiarySnapshot current, SleepHygieneNotesUpdated updated) =>
        current with { SleepDiary = SleepDiary.Advance(current.SleepDiary, SleepDiaryEvent.FromPayload(updated)) };
}
