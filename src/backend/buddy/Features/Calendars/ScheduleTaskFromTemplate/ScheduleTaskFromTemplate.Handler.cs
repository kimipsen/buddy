using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Groups;
using buddy.Features.Guardians;
using buddy.Features.TaskLibrary;
using buddy.Features.Users;

using FluentValidation;

namespace buddy.Features.Calendars;

public static class ScheduleTaskFromTemplateHandler
{
    public static async Task<Result<CalendarItem>> Handle(
        ScheduleTaskFromTemplate command,
        IValidator<ScheduleTaskFromTemplate> validator,
        ICalendarEventStore calendars,
        ICalendarItemEventStore items,
        ITaskTemplateEventStore templates,
        IGroupEventStore groups,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<CalendarItem>.Validation(problem);
        }

        if (command.UserId is not { } userId)
        {
            return new Result<CalendarItem>.NotFound();
        }

        var calendarEvents = await calendars.ReadAsync(command.CalendarId, cancellationToken);
        var calendar = Calendar.Rehydrate(calendarEvents);
        var access = await CalendarAuthorization.CheckContribute(calendar, userId, groups, guardians, cancellationToken);

        if (access != CalendarAccess.Allowed)
        {
            return access.ToDeniedResult<CalendarItem>();
        }

        // Same state-dependent assignee check CreateItemHandler runs, after authorization, using
        // the same already-loaded calendar.
        if (command.AssignedTo is { } assignedTo)
        {
            var assigneeAccess = await CalendarAuthorization.CheckView(calendar, assignedTo, groups, guardians, cancellationToken);

            if (assigneeAccess != CalendarAccess.Allowed)
            {
                return new Result<CalendarItem>.Validation(ValidationProblem.Of("The assigned person doesn't have access to this calendar."));
            }
        }

        var templateId = new TaskTemplateId(command.TaskTemplateId);
        var templateEvents = await templates.ReadAsync(templateId, cancellationToken);
        var template = TaskTemplate.Rehydrate(templateEvents);

        if (template is null)
        {
            return new Result<CalendarItem>.NotFound();
        }

        if (template.IsArchived)
        {
            return new Result<CalendarItem>.Validation(ValidationProblem.Of("Cannot schedule an archived task template."));
        }

        if (template.Subtasks.Count == 0)
        {
            return new Result<CalendarItem>.Validation(ValidationProblem.Of("This task template has no subtasks yet."));
        }

        // The target CalendarId can be group-owned and span more than one family, so the owning
        // child can't be resolved from the calendar -- it's resolved from whoever the task ends
        // up assigned to instead (falling back to the caller when unassigned). The selected
        // template must belong to that exact child, not just anyone with calendar access.
        var owningChildPivot = command.AssignedTo ?? userId;
        var templateOwnerId = await templates.FindChildIdForTemplateAsync(templateId, cancellationToken);

        if (templateOwnerId != owningChildPivot)
        {
            return new Result<CalendarItem>.NotFound();
        }

        // A template-scheduled task is always a specific-time DueDate, never all-day -- IsAllDay
        // isn't exposed on this command's shape at all (unlike CreateItem's), so this is always
        // false rather than caller-supplied.
        var dueDate = new DueDate(command.StartDate, command.StartTime, IsAllDay: false);

        var itemId = CalendarItemId.New();
        var now = DateTimeOffset.UtcNow;

        var created = new TaskItemCreated(
            itemId, command.CalendarId, userId, command.Title, command.Icon, command.Color, dueDate, command.Recurrence, now,
            command.AssignedTo, command.TaskTemplateId);

        var events = await items.CreateAsync(itemId, [created], cancellationToken);

        return new Result<CalendarItem>.Success(CalendarItem.Rehydrate(events)!);
    }
}
