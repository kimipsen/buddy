using buddy.Features.Users;

namespace buddy.Features.Calendars;

// The schedule a CreateItem request asks for, before validation turns StartsAt/EndsAt into a
// Period (end-after-start is a CreateItemValidator rule, reported as a 400). An event carries its
// raw span; a task its due date and optional assignee.
public union NewItemSchedule(NewItemSchedule.Event, NewItemSchedule.Task)
{
    public sealed record Event(StartsAt StartsAt, EndsAt EndsAt, bool IsAllDay);

    public sealed record Task(DueDate DueDate, UserId? AssignedTo);
}

// The new timing a RescheduleItem request asks for -- it must match the item's own case (an event
// is rescheduled with an event timing, a task with a task timing). The assignee isn't part of a
// reschedule.
public union ItemTiming(ItemTiming.Event, ItemTiming.Task)
{
    public sealed record Event(StartsAt StartsAt, EndsAt EndsAt, bool IsAllDay);

    public sealed record Task(DueDate DueDate);
}
