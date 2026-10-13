using buddy.Features.Users;

namespace buddy.Features.Feelings;

// One check-in on the wire. IsSelfReport is derived (LoggedBy == the child), so every view can
// draw the child's own words differently from a guardian's observation.
public sealed record FeelingCheckInResponse(
    Guid Id,
    FeelingZone Zone,
    IReadOnlyList<FeelingTag> Tags,
    string Note,
    DateOnly Date,
    TimeOnly Time,
    Guid LoggedBy,
    bool IsSelfReport,
    DateTimeOffset RecordedAt)
{
    public static FeelingCheckInResponse From(FeelingCheckIn checkIn, UserId childId) => new(
        checkIn.Id.Value,
        checkIn.Zone,
        checkIn.Tags,
        checkIn.Note,
        checkIn.LocalDate,
        checkIn.LocalTime,
        checkIn.LoggedBy.Value,
        checkIn.IsSelfReportOf(childId),
        checkIn.RecordedAt);
}

// The child's own day: today's date in their time zone, the prompt times, the display mode and
// their own self-reports for today -- never a guardian's observation (Question 5).
public sealed record MyFeelingsTodayResponse(
    DateOnly Date,
    IReadOnlyList<TimeOnly> PromptTimes,
    FeelingsDisplayMode DisplayMode,
    IReadOnlyList<FeelingCheckInResponse> CheckIns);

// A guardian's range read, with the child's two check-in settings so the page needs no other GET.
public sealed record FeelingCheckInsResponse(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<TimeOnly> PromptTimes,
    FeelingsDisplayMode DisplayMode,
    IReadOnlyList<FeelingCheckInResponse> CheckIns)
{
    public static FeelingCheckInsResponse ForRange(FeelingsJournal? journal, UserId childId, DateOnly from, DateOnly to) => journal is null
        ? new FeelingCheckInsResponse(from, to, [], FeelingsDisplayMode.FacesAndWords, [])
        : new FeelingCheckInsResponse(
            from,
            to,
            journal.PromptTimes,
            journal.DisplayMode,
            [.. journal.Between(from, to).Select(c => FeelingCheckInResponse.From(c, childId))]);
}

public sealed record PromptTimesResponse(IReadOnlyList<TimeOnly> Times);

public sealed record DisplayModeResponse(FeelingsDisplayMode DisplayMode);

// A link as the guardian sees it in the list -- never the plaintext token, which only
// CreateFeelingsShareLink returns, once.
public sealed record FeelingsShareLinkSummary(Guid Id, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt)
{
    public static FeelingsShareLinkSummary From(FeelingsShareTokenDocument document) =>
        new(document.Id, document.CreatedAt, document.ExpiresAt);
}

internal static class FeelingsShareLinks
{
    public static bool IsLive(FeelingsShareTokenDocument document, DateTimeOffset now) =>
        !document.IsRevoked && (document.ExpiresAt is null || document.ExpiresAt > now);
}

// The plaintext token is returned exactly once, here.
public sealed record IssuedFeelingsShareLink(FeelingsShareTokenId Id, string Token, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt);

public sealed record SharedFeelings(Name ChildName, DateOnly From, DateOnly To, DateTimeOffset? ExpiresAt, IReadOnlyList<FeelingCheckInResponse> CheckIns);
