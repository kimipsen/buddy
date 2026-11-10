namespace buddy.Features.Users;

// "Today" on a user's own wall clock (User.TimeZoneId) -- for a child, the time zone a guardian set
// (docs/backend/analysis/child-timezone-settings.md). Shared by medicine effect notes (no notes for
// future days) and night duty (the "Tonight" card, the earliest EffectiveFrom).
public static class UserLocalDate
{
    // Null for an unknown or deleted user.
    public static async Task<DateOnly?> TodayAsync(UserId userId, IUserEventStore users, TimeProvider time, CancellationToken cancellationToken)
    {
        var user = await users.FindSnapshotAsync(userId, cancellationToken);

        if (user is null || user.IsDeleted)
        {
            return null;
        }

        return Today(user, time);
    }

    // For a caller that already holds the user's snapshot.
    public static DateOnly Today(User user, TimeProvider time)
    {
        var local = TimeZoneInfo.ConvertTime(time.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(user.TimeZoneId.Value));

        return DateOnly.FromDateTime(local.DateTime);
    }
}
