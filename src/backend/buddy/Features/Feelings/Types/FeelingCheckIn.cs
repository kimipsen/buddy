using buddy.Features.Users;

namespace buddy.Features.Feelings;

// One check-in (docs/backend/analysis/feelings-check-in.md, Question 4). LocalDate/LocalTime are the
// child's wall clock (User.TimeZoneId) at the moment described; RecordedAt is the instant it was
// saved and drives the child's undo window. LoggedBy == the child for a self-report, otherwise it's
// a guardian's observation -- derived, never stored as a flag. Note follows the FreeText
// convention: "" means none.
public sealed record FeelingCheckIn(
    FeelingCheckInId Id,
    FeelingZone Zone,
    IReadOnlyList<FeelingTag> Tags,
    string Note,
    DateOnly LocalDate,
    TimeOnly LocalTime,
    UserId LoggedBy,
    DateTimeOffset RecordedAt)
{
    public bool IsSelfReportOf(UserId childId) => LoggedBy == childId;

    // Record equality compares Tags by reference, so the append-only-if-changed rule walks it.
    // LoggedBy and RecordedAt are ignored: an edit keeps both, so only what was said can differ.
    public bool HasSameContentAs(FeelingCheckIn other) =>
        Zone == other.Zone
        && Note == other.Note
        && LocalDate == other.LocalDate
        && LocalTime == other.LocalTime
        && Tags.SequenceEqual(other.Tags);
}
