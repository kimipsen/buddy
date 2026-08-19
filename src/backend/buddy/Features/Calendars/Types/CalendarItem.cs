using buddy.Features.Users;

namespace buddy.Features.Calendars;

public sealed record CalendarItem(
    CalendarItemId Id,
    CalendarId CalendarId,
    UserId CreatedBy,
    CalendarItemKind Kind,
    string Title,
    Icon Icon,
    Color Color,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    DateTimeOffset? DueAt,
    RecurrenceRule? Recurrence,
    bool IsDeleted = false)
{
    // Sort key for calendar listings: an event sorts by its own start, a task by its due date.
    // Safe to assume non-null -- Rehydrate always sets StartsAt for Event and DueAt for Task.
    public DateTimeOffset ScheduleKey => Kind == CalendarItemKind.Event ? StartsAt!.Value : DueAt!.Value;

    public static CalendarItem? Rehydrate(IEnumerable<CalendarItemEvent> events)
    {
        CalendarItem? item = null;

        foreach (var @event in events)
        {
            item = @event switch
            {
                EventItemCreated created => new CalendarItem(
                    created.Id,
                    created.CalendarId,
                    created.CreatedBy,
                    CalendarItemKind.Event,
                    created.Title,
                    created.Icon,
                    created.Color,
                    created.StartsAt,
                    created.EndsAt,
                    null,
                    created.Recurrence),
                TaskItemCreated created => new CalendarItem(
                    created.Id,
                    created.CalendarId,
                    created.CreatedBy,
                    CalendarItemKind.Task,
                    created.Title,
                    created.Icon,
                    created.Color,
                    null,
                    null,
                    created.DueAt,
                    created.Recurrence),
                ItemDetailsUpdated updated => item! with { Title = updated.After.Title, Icon = updated.After.Icon, Color = updated.After.Color },
                EventRescheduled rescheduled => item! with { StartsAt = rescheduled.AfterStartsAt, EndsAt = rescheduled.AfterEndsAt },
                TaskRescheduled rescheduled => item! with { DueAt = rescheduled.AfterDueAt },
                RecurrenceUpdated recurrence => item! with { Recurrence = recurrence.After },
                ItemDeleted => item! with { IsDeleted = true },
                _ => item
            };
        }

        return item;
    }
}
