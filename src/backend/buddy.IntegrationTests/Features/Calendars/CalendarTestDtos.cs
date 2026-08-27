using buddy.Features.Calendars;

namespace buddy.IntegrationTests.Features.Calendars;

// Shared response shapes for the Calendars endpoint tests, matching CalendarResponse /
// CalendarItemResponse (Features/Calendars/GetCalendar and CreateItem). Strongly-typed ids
// serialize as a raw Guid (StronglyTypedIdJsonConverterFactory).
internal sealed record CalendarResponseDto(Guid Id, string Name, string Icon, string TimeZoneId, IReadOnlyCollection<CalendarMemberDto> Members);

internal sealed record CalendarMemberDto(Guid UserId, CalendarRole Role);

internal sealed record CalendarSummaryDto(Guid Id, string Name, string Icon, CalendarRole Role);

internal sealed record RecurrenceRuleDto(RecurrenceFrequency Frequency, int IntervalCount, DateOnly? Until);

// Icon is null when the item has no override -- it inherits the owning calendar's icon (see
// CalendarItemResponse). The always-resolved value only shows up on CalendarItemOccurrenceDto.
internal sealed record CalendarItemDto(
    Guid Id,
    Guid CalendarId,
    CalendarItemKind Kind,
    string Title,
    string? Icon,
    string Color,
    Period? Period,
    DueDate? DueDate,
    RecurrenceRuleDto? Recurrence,
    Guid CreatedBy,
    Guid LastModifiedBy,
    Guid? AssignedTo);

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
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    DateTimeOffset? DueAt,
    bool IsAllDay,
    bool IsCompleted,
    Guid CreatedBy,
    Guid LastModifiedBy,
    Guid? AssignedTo,
    string? ParentTitle,
    Guid? SubtaskId);
