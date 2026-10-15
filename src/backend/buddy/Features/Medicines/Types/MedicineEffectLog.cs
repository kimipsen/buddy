using System.Collections.Immutable;

using buddy.Common.Aggregates;
using buddy.Features.Users;

namespace buddy.Features.Medicines;

// One log per child, keyed by the child's local date: at most one note a day
// (docs/backend/analysis/medicine-effect-notes.md, Question 2).
public sealed record MedicineEffectLog(
    MedicineEffectLogId Id,
    UserId ChildId,
    ImmutableDictionary<DateOnly, MedicineEffectNote> Notes)
{
    public static MedicineEffectLog? Rehydrate(IEnumerable<MedicineEffectLogEvent> events) => EventReplay.Rehydrate(events, Start, Advance);

    public static MedicineEffectLog Replay(IEnumerable<MedicineEffectLogEvent> events) => EventReplay.Replay(events, Start, Advance);

    // Not named Apply/Create/Evolve -- JasperFx's projection source generator scans those names on
    // any projection document type (docs/backend/analysis/event-stream-snapshots.md, Question 4/5).
    public static MedicineEffectLog Start(MedicineEffectLogEvent @event) => @event switch
    {
        MedicineEffectLogStarted started => new MedicineEffectLog(
            started.Id,
            started.ChildId,
            ImmutableDictionary<DateOnly, MedicineEffectNote>.Empty),
        _ => throw EventReplay.NotAStartEvent(nameof(MedicineEffectLog), @event.EventType)
    };

    public static MedicineEffectLog Advance(MedicineEffectLog log, MedicineEffectLogEvent @event) => @event switch
    {
        MedicineEffectNoteLogged logged => log with { Notes = log.Notes.SetItem(logged.Date, logged.After) },
        MedicineEffectNoteCleared cleared => log with { Notes = log.Notes.Remove(cleared.Date) },
        MedicineEffectLogStarted => throw EventReplay.AlreadyStarted(nameof(MedicineEffectLog), @event.EventType)
    };
}
