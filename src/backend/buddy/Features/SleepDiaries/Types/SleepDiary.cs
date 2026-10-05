using System.Collections.Immutable;

using buddy.Common.Aggregates;
using buddy.Features.Users;

namespace buddy.Features.SleepDiaries;

// One unbounded log per child (no 14-day "rounds" -- the reader picks a date range), keyed by date.
// Weekend isn't stored: it's Date.DayOfWeek, derived at read time.
public sealed record SleepDiary(
    SleepDiaryId Id,
    UserId ChildId,
    ImmutableDictionary<DateOnly, SleepEntry> Entries,
    string SleepHygieneNotes)
{
    public static SleepDiary? Rehydrate(IEnumerable<SleepDiaryEvent> events) => EventReplay.Rehydrate(events, Start, Advance);

    public static SleepDiary Replay(IEnumerable<SleepDiaryEvent> events) => EventReplay.Replay(events, Start, Advance);

    // Not named Apply/Create/Evolve -- JasperFx's projection source generator scans those names on
    // any projection document type (docs/backend/analysis/event-stream-snapshots.md, Question 4/5).
    public static SleepDiary Start(SleepDiaryEvent @event) => @event switch
    {
        SleepDiaryStarted started => new SleepDiary(
            started.Id,
            started.ChildId,
            ImmutableDictionary<DateOnly, SleepEntry>.Empty,
            ""),
        _ => throw EventReplay.NotAStartEvent(nameof(SleepDiary), @event.EventType)
    };

    public static SleepDiary Advance(SleepDiary diary, SleepDiaryEvent @event) => @event switch
    {
        // Sparse: a date nobody has logged has no key.
        SleepEntryLogged logged => diary with { Entries = diary.Entries.SetItem(logged.Date, logged.After) },
        SleepEntryCleared cleared => diary with { Entries = diary.Entries.Remove(cleared.Date) },
        SleepHygieneNotesUpdated updated => diary with { SleepHygieneNotes = updated.After },
        SleepDiaryStarted => throw EventReplay.AlreadyStarted(nameof(SleepDiary), @event.EventType)
    };
}
