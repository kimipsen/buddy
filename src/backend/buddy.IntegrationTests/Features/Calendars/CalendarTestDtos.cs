using buddy.Features.Calendars;

namespace buddy.IntegrationTests.Features.Calendars;

// Shared response shapes for the Calendars endpoint tests, matching CalendarResponse /
// CalendarItemResponse (Features/Calendars/GetCalendar and CreateItem). Strongly-typed ids
// serialize as a raw Guid (StronglyTypedIdJsonConverterFactory).
internal sealed record CalendarResponseDto(Guid Id, string Name, string Icon, string TimeZoneId, IReadOnlyCollection<CalendarMemberDto> Members);

internal sealed record CalendarMemberDto(Guid UserId, CalendarRole Role);

internal sealed record CalendarSummaryDto(Guid Id, string Name, string Icon, CalendarRole Role);

internal sealed record RecurrenceRuleDto(RecurrenceFrequency Frequency, int IntervalCount, DateOnly? Until, IReadOnlyList<DayOfWeek>? Weekdays = null);

// Icon is null when the item has no override -- it inherits the owning calendar's icon (see
// CalendarItemResponse). The always-resolved value only shows up on CalendarItemOccurrenceDto.
internal sealed record CalendarItemDto(
    Guid Id,
    Guid CalendarId,
    string Title,
    string? Icon,
    string Color,
    ItemScheduleDto Schedule,
    RecurrenceRuleDto? Recurrence,
    Guid CreatedBy,
    Guid LastModifiedBy)
{
    // Shortcuts into Schedule for the assertions -- the response itself nests them by kind.
    public CalendarItemKind Kind => Schedule.Kind;

    public Period? Period => Schedule.Period;

    public DueDate? DueDate => Schedule.DueDate;

    public Guid? AssignedTo => Schedule.AssignedTo;
}

// ItemScheduleResponse read flat: "kind" plus whichever case fields the response carries.
internal sealed record ItemScheduleDto(
    CalendarItemKind Kind,
    Period? Period = null,
    DueDate? DueDate = null,
    Guid? AssignedTo = null,
    TaskSourceDto? Source = null);

internal sealed record TaskSourceDto(int Kind, Guid? TaskTemplateId = null);

internal sealed record AssignableMemberDto(Guid UserId, string GivenName, string FamilyName);

internal sealed record IcalTokenResponseDto(Guid TokenId, string Token, string SubscriptionPath);

internal sealed record IcalTokenSummaryDto(Guid TokenId, DateTimeOffset IssuedAt);

internal sealed record TaskCompletionResponseDto(Guid ItemId, DateOnly OccurrenceDate, bool IsCompleted);

internal sealed record CalendarItemOccurrenceDto(
    Guid ItemId,
    CalendarItemKind Kind,
    string Title,
    string Icon,
    string? IconOverride,
    string Color,
    OccurrenceTimingDto Timing,
    DateTimeOffset SortAt,
    bool IsAllDay,
    bool IsCompleted,
    Guid CreatedBy,
    Guid LastModifiedBy,
    Guid? AssignedTo,
    RoutineDto? Routine)
{
    // Shortcuts into Timing and Routine for the assertions -- the response itself nests them.
    public DateTimeOffset? StartsAt => Timing.StartsAt;

    public DateTimeOffset? EndsAt => Timing.EndsAt;

    public DateTimeOffset? DueAt => Timing.DueAt;

    public Guid? SubtaskId => Routine?.SubtaskId;

    public string? ParentTitle => Routine?.ParentTitle;

    public string? ParentIcon => Routine?.ParentIcon;
}

// OccurrenceTiming read flat: "kind" (0 timed, 1 due) plus whichever case fields it carries.
internal sealed record OccurrenceTimingDto(
    int Kind,
    DateTimeOffset? StartsAt = null,
    DateTimeOffset? EndsAt = null,
    DateTimeOffset? DueAt = null);

internal sealed record RoutineDto(Guid SubtaskId, string ParentTitle, string ParentIcon);
