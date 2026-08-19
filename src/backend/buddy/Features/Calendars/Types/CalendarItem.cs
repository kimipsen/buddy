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
    Period? Period,
    DueDate? DueDate,
    RecurrenceRule? Recurrence,
    UserId LastModifiedBy,
    bool IsDeleted = false)
{
    // Sort key for calendar listings: an event sorts by its own start, a task by its due date.
    // A plain local DateTime is fine here -- it's only used to order items within one calendar,
    // which all share the same time zone, not to resolve an actual instant.
    // Safe to assume non-null -- Rehydrate always sets Period for Event and DueDate for Task.
    public DateTime ScheduleKey => Kind == CalendarItemKind.Event
        ? Period!.StartsAt.Date.ToDateTime(Period.StartsAt.Time)
        : DueDate!.Date.ToDateTime(DueDate.Time);

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
                    created.Period,
                    null,
                    created.Recurrence,
                    created.CreatedBy),
                TaskItemCreated created => new CalendarItem(
                    created.Id,
                    created.CalendarId,
                    created.CreatedBy,
                    CalendarItemKind.Task,
                    created.Title,
                    created.Icon,
                    created.Color,
                    null,
                    created.DueDate,
                    created.Recurrence,
                    created.CreatedBy),
                ItemDetailsUpdated updated => item! with { Title = updated.After.Title, Icon = updated.After.Icon, Color = updated.After.Color, LastModifiedBy = updated.ModifiedBy },
                EventRescheduled rescheduled => item! with { Period = rescheduled.After, LastModifiedBy = rescheduled.ModifiedBy },
                TaskRescheduled rescheduled => item! with { DueDate = rescheduled.After, LastModifiedBy = rescheduled.ModifiedBy },
                RecurrenceUpdated recurrence => item! with { Recurrence = recurrence.After, LastModifiedBy = recurrence.ModifiedBy },
                ItemDeleted deleted => item! with { IsDeleted = true, LastModifiedBy = deleted.ModifiedBy },
                _ => item
            };
        }

        return item;
    }
}
