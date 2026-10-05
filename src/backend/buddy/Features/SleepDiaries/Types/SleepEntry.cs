using buddy.Features.Users;

namespace buddy.Features.SleepDiaries;

// One day's row of the clinical sleep-registration form. Every field but IsTired/LoggedBy is
// optional: a guardian writing up the night the next morning saves what they remember and fills in
// the rest later (docs/backend/analysis/sleep-diary.md, Question 3). Remarks follows the FreeText
// convention -- "" means none given.
public sealed record SleepEntry(
    TimeOnly? RoutineStartTime,
    TimeOnly? RitualStartTime,
    TimeOnly? RitualEndTime,
    TimeOnly? BedTime,
    TimeOnly? FellAsleepTime,
    IReadOnlyList<SleepInterval> NightWakeUps,
    TimeOnly? MorningWakeTime,
    bool IsTired,
    IReadOnlyList<SleepInterval> Naps,
    // Guardian-entered, never computed server-side -- parents estimate overnight sleep, so a total
    // derived from the timestamps above would be falsely precise.
    TimeSpan? TotalSleepDuration,
    string Remarks,
    UserId LoggedBy)
{
    // Record equality compares the two lists by reference, so content comparison (for the
    // append-only-if-changed rule) walks them explicitly. LoggedBy is ignored: re-saving the same
    // night by another guardian isn't a change worth a history entry, same rule AssignPickup uses.
    public bool HasSameContentAs(SleepEntry other) =>
        RoutineStartTime == other.RoutineStartTime
        && RitualStartTime == other.RitualStartTime
        && RitualEndTime == other.RitualEndTime
        && BedTime == other.BedTime
        && FellAsleepTime == other.FellAsleepTime
        && MorningWakeTime == other.MorningWakeTime
        && IsTired == other.IsTired
        && TotalSleepDuration == other.TotalSleepDuration
        && Remarks == other.Remarks
        && NightWakeUps.SequenceEqual(other.NightWakeUps)
        && Naps.SequenceEqual(other.Naps);
}

// A night wake-up or a daytime nap -- the paper form records both as a time plus a duration. A
// plain record, not a union: there is only one shape (see Question 3).
public sealed record SleepInterval(TimeOnly StartTime, TimeSpan Duration);
