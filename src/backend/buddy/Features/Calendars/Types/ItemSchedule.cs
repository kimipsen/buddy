using buddy.Features.Users;

namespace buddy.Features.Calendars;

// When a CalendarItem happens. An event spans a Period; a task is due at a DueDate and may be
// assigned to someone and scheduled from a TaskLibrary template. Each case carries only its own
// data, so an event can't have a due date or an assignee -- what CalendarItem's Kind plus nullable
// Period/DueDate/AssignedTo/TaskTemplateId used to leave to validation. Stored in the
// CalendarItem snapshot through ItemScheduleJsonConverter; the events themselves stay union-free
// (EventItemCreated / TaskItemCreated / TemplateTaskItemCreated). See
// docs/backend/analysis/eliminate-nulls.md, Phase 5.2.
public union ItemSchedule(ItemSchedule.Event, ItemSchedule.Task)
{
    public sealed record Event(Period Period);

    // AssignedTo is null for an unassigned task -- a real state, not a missing value.
    public sealed record Task(DueDate DueDate, UserId? AssignedTo, TaskSource Source);
}

// Where a task came from: entered by hand, or scheduled from a TaskLibrary template. The template
// id is a raw Guid, not TaskLibrary's TaskTemplateId -- Calendars takes no compile dependency on
// TaskLibrary's types.
public union TaskSource(TaskSource.Freeform, TaskSource.FromTemplate)
{
    public sealed record Freeform;

    public sealed record FromTemplate(Guid TaskTemplateId);
}
