using System.Collections.Immutable;

namespace buddy.Features.Calendars;

// A calendar item's alarms for subscribed calendar apps: one RFC 5545 VALARM per offset, written by
// IcalFeedWriter. Each offset is signed whole minutes from the occurrence's anchor (an event's
// start, a task's due time, local midnight for an all-day item); negative means before. Buddy never
// fires them itself, and they are unrelated to transition warnings
// (docs/backend/analysis/calendar-item-alarms.md). Kept distinct and ascending, so two selections
// of the same alarms compare equal.
public sealed record ItemAlarms(ImmutableArray<int> OffsetMinutes)
{
    public const int Max = 3;

    // 4 weeks before the anchor.
    public const int MinOffset = -4 * 7 * 24 * 60;

    // Up to 24 h after the anchor, for an all-day item's "on the day at 09:00" (+540).
    public const int MaxOffset = 24 * 60;

    public static readonly ItemAlarms None = new([]);

    // Duplicates are merged, not rejected, the same as LeadTimeRules.Normalize.
    public static ItemAlarms From(IEnumerable<int>? offsetMinutes) =>
        offsetMinutes is null ? None : new([.. offsetMinutes.Distinct().Order()]);

    // ImmutableArray compares by reference; an identical selection is a no-op.
    public bool Equals(ItemAlarms? other) => other is not null && OffsetMinutes.SequenceEqual(other.OffsetMinutes);

    public override int GetHashCode() => OffsetMinutes.Length;
}
