using System.Collections.Immutable;

using buddy.Common.Aggregates;
using buddy.Features.Users;

namespace buddy.Features.NightDuty;

public sealed record NightSchedule(
    NightScheduleId Id,
    UserId ChildId,
    // Fixed-schedule versions, ordered by EffectiveFrom (NightPatternReplaced keeps it sorted).
    ImmutableList<NightPattern> Patterns,
    // Sparse: only dates with an explicit exception hold a key, like WorkLocationSchedule.Overrides.
    ImmutableDictionary<DateOnly, NightOverride> Overrides)
{
    // What a child whose guardians have never written anything looks like -- the same state
    // NightScheduleStarted folds to, so handlers can treat "no stream yet" uniformly.
    public static NightSchedule Empty(UserId childId) => new(
        NightScheduleId.ForChild(childId),
        childId,
        [],
        ImmutableDictionary<DateOnly, NightOverride>.Empty);

    // The version in effect on date: the latest one starting on or before it. Null before the first.
    public NightPattern? PatternFor(DateOnly date) => Patterns.LastOrDefault(p => p.EffectiveFrom <= date);

    // What the fixed schedule says for date, ignoring overrides.
    public NightEntry? ScheduledEntryFor(DateOnly date) => PatternFor(date)?.EntryFor(date);

    // "From this date on, this is the schedule": drops every version starting on or after it.
    public ImmutableList<NightPattern> WithPattern(NightPattern pattern) =>
        [.. Patterns.Where(p => p.EffectiveFrom < pattern.EffectiveFrom), pattern];

    public static NightSchedule? Rehydrate(IEnumerable<NightDutyEvent> events) => EventReplay.Rehydrate(events, Start, Advance);

    public static NightSchedule Replay(IEnumerable<NightDutyEvent> events) => EventReplay.Replay(events, Start, Advance);

    // Not named Apply/Create -- see WorkLocationSchedule.Start for why.
    public static NightSchedule Start(NightDutyEvent @event) => @event switch
    {
        NightScheduleStarted started => Empty(started.ChildId),
        _ => throw EventReplay.NotAStartEvent(nameof(NightSchedule), @event.EventType)
    };

    public static NightSchedule Advance(NightSchedule schedule, NightDutyEvent @event) => @event switch
    {
        NightPatternReplaced replaced => schedule with { Patterns = schedule.WithPattern(replaced.Pattern) },
        NightOverridden overridden => schedule with
        {
            Overrides = schedule.Overrides.SetItem(overridden.Date, overridden.Override)
        },
        NightOverrideCleared cleared => schedule with
        {
            Overrides = schedule.Overrides.Remove(cleared.Date)
        },
        NightScheduleStarted => throw EventReplay.AlreadyStarted(nameof(NightSchedule), @event.EventType)
    };
}
