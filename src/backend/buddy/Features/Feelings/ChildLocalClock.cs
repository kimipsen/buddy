using buddy.Features.Users;

namespace buddy.Features.Feelings;

// "Now" on the child's own wall clock (User.TimeZoneId), the server-side twin of the frontend's
// ChildClockService: a check-in's LocalDate/LocalTime and "today" on the child's card both come from
// here (docs/backend/analysis/feelings-check-in.md, Question 4).
public sealed record ChildLocalNow(DateTimeOffset Instant, DateOnly Date, TimeOnly Time);

public static class ChildLocalClock
{
    // Null for an unknown or deleted child.
    public static async Task<ChildLocalNow?> NowAsync(UserId childId, IUserEventStore users, TimeProvider time, CancellationToken cancellationToken)
    {
        var child = await users.FindSnapshotAsync(childId, cancellationToken);

        if (child is null || child.IsDeleted)
        {
            return null;
        }

        return Now(child, time);
    }

    public static ChildLocalNow Now(User child, TimeProvider time)
    {
        var instant = time.GetUtcNow();
        var local = TimeZoneInfo.ConvertTime(instant, TimeZoneInfo.FindSystemTimeZoneById(child.TimeZoneId.Value));

        // Whole minutes: the moment a check-in describes is "14:30", not "14:30:17.204".
        return new ChildLocalNow(
            instant,
            DateOnly.FromDateTime(local.DateTime),
            new TimeOnly(local.Hour, local.Minute));
    }
}
