using System.Diagnostics;
using System.Text.Json.Serialization;

using buddy.Serialization;

namespace buddy.Features.Calendars;

// A computed instance of a CalendarItem's recurrence rule, resolved to an actual instant via the
// owning Calendar's TimeZoneId. Never persisted -- always recomputed from current item/calendar
// state (see CalendarOccurrenceExpansion), shared by ListOccurrences, the ical feed and the meal
// plan assistant. Icon is always the effective value (the item's own override, or the calendar's
// default when it has none) -- IconOverride carries the raw, possibly-null per-item value so an
// edit form can tell "inheriting" apart from "explicitly set to the same emoji as the default".
// For a routine's subtask, IconOverride is the parent item's override: a subtask has no override
// of its own to edit from the calendar.
public sealed record CalendarItemOccurrence(
    CalendarItemId ItemId,
    CalendarItemKind Kind,
    string Title,
    string Icon,
    string? IconOverride,
    string Color,
    OccurrenceTiming Timing,
    bool IsAllDay,
    bool IsCompleted,
    Guid CreatedBy,
    Guid LastModifiedBy,
    Guid? AssignedTo,
    // Set only when this occurrence is one subtask of a template-scheduled task; null for every
    // other occurrence, which is the normal case.
    Routine? Routine)
{
    // The instant an occurrence sorts and is dated by: an event's or a routine subtask's start, a
    // plain task's due instant.
    public DateTimeOffset SortAt => Timing switch
    {
        OccurrenceTiming.Timed timed => timed.StartsAt,
        OccurrenceTiming.Due due => due.DueAt,
        _ => throw new UnreachableException($"Unmapped OccurrenceTiming case: {Timing.GetType().Name}."),
    };
}

// An event occurrence and each subtask of a routine span a window; a plain task is due at one
// instant. Wire shape { "kind": 0, "startsAt", "endsAt" } or { "kind": 1, "dueAt" }.
[JsonConverter(typeof(OccurrenceTimingJsonConverter))]
public abstract record OccurrenceTiming
{
    public sealed record Timed(DateTimeOffset StartsAt, DateTimeOffset EndsAt) : OccurrenceTiming;

    public sealed record Due(DateTimeOffset DueAt) : OccurrenceTiming;
}

public sealed class OccurrenceTimingJsonConverter : KindDiscriminatedJsonConverter<OccurrenceTiming>
{
    protected override IReadOnlyDictionary<int, Type> Cases { get; } = new Dictionary<int, Type>
    {
        [0] = typeof(OccurrenceTiming.Timed),
        [1] = typeof(OccurrenceTiming.Due),
    };
}

// One subtask of a template-scheduled task. Title on the occurrence is the subtask's own title;
// ParentTitle and ParentIcon (the parent's effective icon) let a client group a routine's subtask
// occurrences under their shared parent, whose header shouldn't take any one subtask's icon.
// SubtaskId targets the subtask completion route.
public sealed record Routine(Guid SubtaskId, string ParentTitle, string ParentIcon);
