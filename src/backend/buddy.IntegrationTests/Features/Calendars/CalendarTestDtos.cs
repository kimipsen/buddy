using buddy.Features.Calendars;

namespace buddy.IntegrationTests.Features.Calendars;

// Shared response shapes for the Calendars endpoint tests, matching CalendarResponse /
// CalendarItemResponse (Features/Calendars/GetCalendar and CreateItem). Strongly-typed ids
// serialize as a raw Guid (StronglyTypedIdJsonConverterFactory).
internal sealed record CalendarResponseDto(Guid Id, string Name, string TimeZoneId, IReadOnlyCollection<CalendarMemberDto> Members);

internal sealed record CalendarMemberDto(Guid UserId, CalendarRole Role);

internal sealed record CalendarSummaryDto(Guid Id, string Name, CalendarRole Role);

internal sealed record RecurrenceRuleDto(RecurrenceFrequency Frequency, int IntervalCount, DateOnly? Until);

internal sealed record CalendarItemDto(
    Guid Id,
    Guid CalendarId,
    CalendarItemKind Kind,
    string Title,
    string Icon,
    string Color,
    Period? Period,
    DueDate? DueDate,
    RecurrenceRuleDto? Recurrence,
    Guid CreatedBy,
    Guid LastModifiedBy);

internal sealed record IcalTokenResponseDto(Guid TokenId, string Token, string SubscriptionPath);

internal sealed record IcalTokenSummaryDto(Guid TokenId, DateTimeOffset IssuedAt);
