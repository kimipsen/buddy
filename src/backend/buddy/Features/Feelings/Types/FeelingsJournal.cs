using System.Collections.Immutable;

using buddy.Common.Aggregates;
using buddy.Features.Users;

namespace buddy.Features.Feelings;

// One journal per child, check-ins keyed by id (several a day, each edited, undone or removed on its
// own), plus the child's two check-in settings: the prompt times and the display mode
// (docs/backend/analysis/feelings-check-in.md, Questions 3, 6 and 9).
public sealed record FeelingsJournal(
    FeelingsJournalId Id,
    UserId ChildId,
    ImmutableDictionary<FeelingCheckInId, FeelingCheckIn> CheckIns,
    ImmutableList<TimeOnly> PromptTimes,
    FeelingsDisplayMode DisplayMode)
{
    public static FeelingsJournal? Rehydrate(IEnumerable<FeelingsJournalEvent> events) => EventReplay.Rehydrate(events, Start, Advance);

    public static FeelingsJournal Replay(IEnumerable<FeelingsJournalEvent> events) => EventReplay.Replay(events, Start, Advance);

    // Not named Apply/Create/Evolve -- JasperFx's projection source generator scans those names on
    // any projection document type (docs/backend/analysis/event-stream-snapshots.md, Question 4/5).
    public static FeelingsJournal Start(FeelingsJournalEvent @event) => @event switch
    {
        FeelingsJournalStarted started => new FeelingsJournal(
            started.Id,
            started.ChildId,
            ImmutableDictionary<FeelingCheckInId, FeelingCheckIn>.Empty,
            [],
            FeelingsDisplayMode.FacesAndWords),
        _ => throw EventReplay.NotAStartEvent(nameof(FeelingsJournal), @event.EventType)
    };

    public static FeelingsJournal Advance(FeelingsJournal journal, FeelingsJournalEvent @event) => @event switch
    {
        FeelingCheckedIn checkedIn => journal with { CheckIns = journal.CheckIns.SetItem(checkedIn.CheckIn.Id, checkedIn.CheckIn) },
        FeelingCheckInEdited edited => journal with { CheckIns = journal.CheckIns.SetItem(edited.After.Id, edited.After) },
        FeelingCheckInRemoved removed => journal with { CheckIns = journal.CheckIns.Remove(removed.Before.Id) },
        CheckInPromptTimesSet set => journal with { PromptTimes = [.. set.After] },
        FeelingsDisplayModeSet set => journal with { DisplayMode = set.After },
        FeelingsJournalStarted => throw EventReplay.AlreadyStarted(nameof(FeelingsJournal), @event.EventType)
    };

    // Sorted by the moment described, then by when it was saved -- the order every list shows.
    public IEnumerable<FeelingCheckIn> Between(DateOnly from, DateOnly to) =>
        CheckIns.Values
            .Where(c => c.LocalDate >= from && c.LocalDate <= to)
            .OrderBy(c => c.LocalDate)
            .ThenBy(c => c.LocalTime)
            .ThenBy(c => c.RecordedAt);

    public int CountOn(DateOnly date) => CheckIns.Values.Count(c => c.LocalDate == date);
}
