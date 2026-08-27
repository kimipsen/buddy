namespace buddy.Features.Calendars;

// A computed instance of a CalendarItem's recurrence rule, resolved to an actual instant via the
// owning Calendar's TimeZoneId. Never persisted -- always recomputed from current item/calendar
// state (see CalendarOccurrenceExpansion), shared by ListOccurrences and the ical feed. Icon is
// always the effective value (the item's own override, or the calendar's default when it has
// none) -- IconOverride carries the raw, possibly-null per-item value so an edit form can tell
// "inheriting" apart from "explicitly set to the same emoji as the default".
public sealed record CalendarItemOccurrence(
    CalendarItemId ItemId,
    CalendarItemKind Kind,
    string Title,
    string Icon,
    string? IconOverride,
    string Color,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    DateTimeOffset? DueAt,
    bool IsAllDay,
    bool IsCompleted,
    Guid CreatedBy,
    Guid LastModifiedBy,
    Guid? AssignedTo,
    // The parent item's own Title, set only when this occurrence is one subtask of a
    // template-scheduled task (Title above is the subtask's own title in that case) -- lets the
    // frontend group a routine's subtask occurrences under their shared parent. Null for every
    // other occurrence. Additive trailing field: never persisted, so there's no golden-file/replay
    // concern, only "don't break existing JSON consumers", which a new optional field doesn't.
    string? ParentTitle = null,
    // The subtask's own id, set only for a template-scheduled task's per-subtask occurrence --
    // required by SetTaskCompletion to target the right subtask. Null otherwise.
    Guid? SubtaskId = null,
    // The parent item's own effective icon (its override, or the calendar's default when it has
    // none) -- set alongside ParentTitle, for the same reason: Icon above is the *subtask's* own
    // icon (falling back to the parent's, then the calendar's), which can legitimately differ
    // between sibling subtasks, so it's the wrong value for the group's own header. Null for every
    // other occurrence.
    string? ParentIcon = null);
