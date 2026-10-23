using System.Globalization;

using buddy.Common.Ical;
using buddy.Features.Calendars;
using buddy.Features.Users;

using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;

using IcsCalendar = Ical.Net.Calendar;
using IcsEvent = Ical.Net.CalendarComponents.CalendarEvent;

namespace buddy.Features.Pickups;

// One slot of the feed: a PickupOccurrence plus what the writer needs about its child.
public sealed record PickupFeedEntry(UserId ChildId, string ChildName, TimeZoneId ChildTimeZone, PickupOccurrence Occurrence);

// Modeled on Features/Calendars/IcalFeedWriter.cs and MealPlanIcalFeedWriter.cs. See
// docs/backend/analysis/pickup-ical-feed.md, Questions 3-5.
public static class PickupIcalFeedWriter
{
    // A drop-off or pickup is a moment, not a block.
    private static readonly TimeSpan EventDuration = TimeSpan.FromMinutes(15);

    private static readonly Duration ReminderBefore = new(minutes: -30);

    // The only text the backend writes for a person to read: the feed has no frontend to translate
    // it. Keyed by User.Language; an unknown language falls back to English.
    private sealed record FeedTexts(string FeedName, string DropOff, string PickUp);

    private static readonly Dictionary<string, FeedTexts> Texts = new Dictionary<string, FeedTexts>
    {
        ["en"] = new("My pickups", "Drop off {0}", "Pick up {0}"),
        ["da"] = new("Mine afhentninger", "Aflever {0}", "Hent {0}"),
    };

    public static string Write(IReadOnlyCollection<PickupFeedEntry> entries, Language language, TimeProvider time)
    {
        var texts = Texts.GetValueOrDefault(language.Value) ?? Texts[SupportedLanguages.Default.Value];

        var calendar = new IcsCalendar();
        IcalSubscription.Describe(calendar, texts.FeedName);

        // Start of the UTC day, not UtcNow: an unchanged feed must render byte-identically within a
        // day or its ETag never matches (see docs/backend/analysis/conditional-get-etags.md).
        var stamp = new CalDateTime(time.GetUtcNow().UtcDateTime.Date, "UTC");

        foreach (var entry in entries)
        {
            var occurrence = entry.Occurrence;
            var template = occurrence.Slot == PickupSlot.DropOff ? texts.DropOff : texts.PickUp;
            var summary = string.Format(CultureInfo.InvariantCulture, template, entry.ChildName);

            var icsEvent = new IcsEvent
            {
                Uid = BuildUid(entry),
                Summary = summary,
                // "" means no notes; Ical.Net writes an empty DESCRIPTION line for "" but omits null.
                Description = occurrence.Notes.Length == 0 ? null : occurrence.Notes,
                DtStamp = stamp,
            };

            if (occurrence.Time is { } timeOfDay)
            {
                // The child's own wall-clock time, anchored to their zone and written as UTC like
                // IcalFeedWriter, so a guardian in another zone sees the right moment.
                var start = TimeZoneResolution.ResolveInstant(entry.ChildTimeZone, occurrence.Date.ToDateTime(timeOfDay));
                icsEvent.DtStart = new CalDateTime(start.UtcDateTime, "UTC");
                icsEvent.DtEnd = new CalDateTime(start.Add(EventDuration).UtcDateTime, "UTC");
                icsEvent.Alarms.Add(new Alarm
                {
                    Action = AlarmAction.Display,
                    Description = summary,
                    Trigger = new Trigger(ReminderBefore),
                });
            }
            else
            {
                // No time recorded: an all-day event rather than an invented time, and no alarm,
                // whose trigger would be relative to midnight.
                icsEvent.DtStart = new CalDateTime(occurrence.Date);
                icsEvent.DtEnd = new CalDateTime(occurrence.Date.AddDays(1));
            }

            calendar.Events.Add(icsEvent);
        }

        // SerializeToString is annotated as returning string?, but never actually returns null for
        // a non-null Calendar instance.
        return new CalendarSerializer().SerializeToString(calendar)!;
    }

    // Stable per (child, date, slot): a changed time updates the event in place, and a slot
    // reassigned to someone else disappears from this feed.
    private static string BuildUid(PickupFeedEntry entry) =>
        $"{entry.ChildId.Value:N}-{entry.Occurrence.Date:yyyyMMdd}-{entry.Occurrence.Slot}@buddy-pickup";
}
