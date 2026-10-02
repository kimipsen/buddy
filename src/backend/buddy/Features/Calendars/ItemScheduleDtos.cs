using System.Diagnostics;
using System.Text.Json.Serialization;

using buddy.Features.Users;
using buddy.Serialization;

namespace buddy.Features.Calendars;

// HTTP shapes of a calendar item's schedule. Each is an object whose numeric "kind" matches
// CalendarItemKind (0 = event, 1 = task), carrying only that case's fields -- see
// KindDiscriminatedJsonConverter. A task's due date travels as { date, time } with all-day-ness as
// the case's own isAllDay, matching how an event's isAllDay sits beside startsAt/endsAt.

public sealed record DueDateRequest(DateOnly Date, TimeOnly Time)
{
    public DueDate ToDueDate(bool isAllDay) => new(Date, Time, isAllDay);
}

// CreateItem: { "kind": 0, "startsAt", "endsAt", "isAllDay" } | { "kind": 1, "dueDate", "isAllDay", "assignedTo"? }
[JsonConverter(typeof(ItemScheduleRequestJsonConverter))]
public abstract record ItemScheduleRequest
{
    public NewItemSchedule ToSchedule() => this switch
    {
        EventScheduleRequest @event => new NewItemSchedule.Event(@event.StartsAt, @event.EndsAt, @event.IsAllDay),
        TaskScheduleRequest task => new NewItemSchedule.Task(
            task.DueDate.ToDueDate(task.IsAllDay), task.AssignedTo is { } assignedTo ? new UserId(assignedTo) : null),
        _ => throw new UnreachableException($"Unmapped ItemScheduleRequest case: {GetType().Name}."),
    };
}

// Disallow: an event has no due date or assignee, so sending one is a 400 rather than silently dropped.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EventScheduleRequest(StartsAt StartsAt, EndsAt EndsAt, bool IsAllDay) : ItemScheduleRequest;

// AssignedTo is optional: null means unassigned.
public sealed record TaskScheduleRequest(DueDateRequest DueDate, bool IsAllDay, Guid? AssignedTo = null) : ItemScheduleRequest;

public sealed class ItemScheduleRequestJsonConverter : KindDiscriminatedJsonConverter<ItemScheduleRequest>
{
    protected override IReadOnlyDictionary<int, Type> Cases { get; } = new Dictionary<int, Type>
    {
        [(int)CalendarItemKind.Event] = typeof(EventScheduleRequest),
        [(int)CalendarItemKind.Task] = typeof(TaskScheduleRequest),
    };
}

// RescheduleItem: { "kind": 0, "startsAt", "endsAt", "isAllDay" } | { "kind": 1, "dueDate", "isAllDay" }
[JsonConverter(typeof(ItemTimingRequestJsonConverter))]
public abstract record ItemTimingRequest
{
    public ItemTiming ToTiming() => this switch
    {
        EventTimingRequest @event => new ItemTiming.Event(@event.StartsAt, @event.EndsAt, @event.IsAllDay),
        TaskTimingRequest task => new ItemTiming.Task(task.DueDate.ToDueDate(task.IsAllDay)),
        _ => throw new UnreachableException($"Unmapped ItemTimingRequest case: {GetType().Name}."),
    };
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EventTimingRequest(StartsAt StartsAt, EndsAt EndsAt, bool IsAllDay) : ItemTimingRequest;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TaskTimingRequest(DueDateRequest DueDate, bool IsAllDay) : ItemTimingRequest;

public sealed class ItemTimingRequestJsonConverter : KindDiscriminatedJsonConverter<ItemTimingRequest>
{
    protected override IReadOnlyDictionary<int, Type> Cases { get; } = new Dictionary<int, Type>
    {
        [(int)CalendarItemKind.Event] = typeof(EventTimingRequest),
        [(int)CalendarItemKind.Task] = typeof(TaskTimingRequest),
    };
}

// CalendarItemResponse.Schedule: { "kind": 0, "period" } | { "kind": 1, "dueDate", "assignedTo", "source" }
[JsonConverter(typeof(ItemScheduleResponseJsonConverter))]
public abstract record ItemScheduleResponse
{
    public static ItemScheduleResponse From(ItemSchedule schedule) => schedule switch
    {
        ItemSchedule.Event @event => new EventScheduleResponse(@event.Period),
        ItemSchedule.Task task => new TaskScheduleResponse(task.DueDate, task.AssignedTo?.Value, TaskSourceResponse.From(task.Source)),
    };
}

public sealed record EventScheduleResponse(Period Period) : ItemScheduleResponse;

// AssignedTo is null for an unassigned task.
public sealed record TaskScheduleResponse(DueDate DueDate, Guid? AssignedTo, TaskSourceResponse Source) : ItemScheduleResponse;

public sealed class ItemScheduleResponseJsonConverter : KindDiscriminatedJsonConverter<ItemScheduleResponse>
{
    protected override IReadOnlyDictionary<int, Type> Cases { get; } = new Dictionary<int, Type>
    {
        [(int)CalendarItemKind.Event] = typeof(EventScheduleResponse),
        [(int)CalendarItemKind.Task] = typeof(TaskScheduleResponse),
    };
}

// { "kind": 0 } (entered by hand) | { "kind": 1, "taskTemplateId" } (scheduled from a template)
[JsonConverter(typeof(TaskSourceResponseJsonConverter))]
public abstract record TaskSourceResponse
{
    public static TaskSourceResponse From(TaskSource source) => source switch
    {
        TaskSource.Freeform => new FreeformTaskSourceResponse(),
        TaskSource.FromTemplate fromTemplate => new TemplateTaskSourceResponse(fromTemplate.TaskTemplateId),
    };
}

public sealed record FreeformTaskSourceResponse : TaskSourceResponse;

public sealed record TemplateTaskSourceResponse(Guid TaskTemplateId) : TaskSourceResponse;

public sealed class TaskSourceResponseJsonConverter : KindDiscriminatedJsonConverter<TaskSourceResponse>
{
    protected override IReadOnlyDictionary<int, Type> Cases { get; } = new Dictionary<int, Type>
    {
        [0] = typeof(FreeformTaskSourceResponse),
        [1] = typeof(TemplateTaskSourceResponse),
    };
}
