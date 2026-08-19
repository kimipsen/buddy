using buddy.Features.Users;

namespace buddy.Features.Calendars;

public enum CalendarAccess
{
    Allowed,
    // The calendar doesn't exist, is deleted, or the caller isn't a member -- collapsed into one
    // outcome so a non-member can't distinguish a private calendar from a missing one.
    NotFound,
    // The caller can see the calendar but lacks the permission tier the operation requires.
    Forbidden
}

public static class CalendarAuthorization
{
    public static CalendarAccess CheckView(Calendar? calendar, UserId userId) =>
        calendar is not null && calendar.CanView(userId) ? CalendarAccess.Allowed : CalendarAccess.NotFound;

    public static CalendarAccess CheckContribute(Calendar? calendar, UserId userId)
    {
        if (calendar is null || !calendar.CanView(userId))
        {
            return CalendarAccess.NotFound;
        }

        return calendar.CanContribute(userId) ? CalendarAccess.Allowed : CalendarAccess.Forbidden;
    }

    public static CalendarAccess CheckOwner(Calendar? calendar, UserId userId)
    {
        if (calendar is null || !calendar.CanView(userId))
        {
            return CalendarAccess.NotFound;
        }

        return calendar.IsOwner(userId) ? CalendarAccess.Allowed : CalendarAccess.Forbidden;
    }
}
