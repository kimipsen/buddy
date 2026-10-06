using Ical.Net;

using IcsCalendar = Ical.Net.Calendar;

namespace buddy.Common.Ical;

// Shared by every iCal feed (Calendars, Mealplans). A feed is a subscription, not a download: the
// calendar app re-fetches the whole file on its own schedule and matches events by UID, so these
// properties only tell it what to call the feed and how often to poll.
public static class IcalSubscription
{
    public const string ContentType = "text/calendar";

    // Every request re-renders the feed, so a shared cache must never hold it (the URL carries the
    // token) and a client cache must revalidate rather than serve a stale copy.
    public const string CacheControl = "private, no-cache";

    // A hint only: Apple Calendar and Outlook honour it, Google Calendar ignores it and polls on its
    // own schedule (several hours).
    private const string RefreshInterval = "PT1H";

    public static void Describe(IcsCalendar calendar, string name)
    {
        // NAME is the RFC 7986 property; X-WR-CALNAME is the older de facto one most clients read.
        // Neither overrides a name the user types when subscribing (Outlook on the web asks for one).
        calendar.AddProperty("NAME", name);
        calendar.AddProperty("X-WR-CALNAME", name);

        var refresh = new CalendarProperty("REFRESH-INTERVAL", RefreshInterval);
        refresh.Parameters.Add("VALUE", "DURATION");
        calendar.AddProperty(refresh);
        calendar.AddProperty("X-PUBLISHED-TTL", RefreshInterval);
    }
}
