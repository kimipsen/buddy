using System.Collections.Immutable;

namespace buddy.Features.TransitionWarnings;

// What a transition warning can be about. Routines and packing lists count as Task.
public enum WarningKind
{
    Event,
    Task,
    DropOff,
    PickUp
}

// A window with no warnings at all, as wall-clock times in the child's own TimeZoneId. From > To
// wraps midnight (21:00-07:00).
public sealed record QuietHours(TimeOnly From, TimeOnly To);

// The rules a lead-time list follows, on the defaults and on a per-item override.
public static class LeadTimeRules
{
    public const int MinMinutes = 1;
    public const int MaxMinutes = 120;
    public const int MaxCount = 3;
    public const int MaxOverrides = 100;

    // Largest first (15, 5, 1): the order the stages come in.
    public static ImmutableArray<int> Normalize(IEnumerable<int> minutes) =>
        [.. minutes.Distinct().OrderDescending()];
}

// A child's warning settings apart from the per-item overrides, replaced as a whole on every save
// (Question 3). Lead minutes are kept largest first and kinds in enum order, so two saves of the
// same choice compare equal.
public sealed record WarningDefaults(
    bool Enabled,
    ImmutableArray<int> LeadMinutes,
    ImmutableArray<WarningKind> Kinds,
    QuietHours? QuietHours)
{
    // Off for a child no guardian has configured; 15/5/1 and every kind pre-filled for when a
    // guardian turns warnings on (settled by the user 2026-10-11).
    public static readonly WarningDefaults Initial = Create(false, [15, 5, 1], Enum.GetValues<WarningKind>(), null);

    public static WarningDefaults Create(bool enabled, IEnumerable<int> leadMinutes, IEnumerable<WarningKind> kinds, QuietHours? quietHours) =>
        new(enabled, LeadTimeRules.Normalize(leadMinutes), [.. kinds.Distinct().Order()], quietHours);

    // ImmutableArray compares by reference, so equality is spelled out: an identical save is a no-op.
    public bool Equals(WarningDefaults? other) =>
        other is not null
        && Enabled == other.Enabled
        && LeadMinutes.SequenceEqual(other.LeadMinutes)
        && Kinds.SequenceEqual(other.Kinds)
        && QuietHours == other.QuietHours;

    public override int GetHashCode() => HashCode.Combine(Enabled, LeadMinutes.Length, Kinds.Length, QuietHours);
}
