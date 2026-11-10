using System.Globalization;

using buddy.Common.Ical;
using buddy.Features.Users;

using Ical.Net.DataTypes;
using Ical.Net.Serialization;

using IcsCalendar = Ical.Net.Calendar;
using IcsEvent = Ical.Net.CalendarComponents.CalendarEvent;

namespace buddy.Features.NightDuty;

// What the writer needs about the feed's child and the people its nights name. GuardianNames maps a
// guardian's id to what the child calls them (CallingName, else the given name); a guardian who is
// missing from it (deleted, erased) is written as "a former guardian".
public sealed record NightFeedContext(
    UserId ChildId,
    string ChildName,
    IReadOnlyDictionary<Guid, string> GuardianNames,
    Language Language);

// One all-day VEVENT per planned night. Modeled on PickupIcalFeedWriter. See
// docs/backend/analysis/night-duty-ical-feed.md, Questions 3-4.
public static class NightIcalFeedWriter
{
    // The feed has no frontend to translate it, so its text lives here, keyed by the guardian's
    // User.Language; an unknown language falls back to English. The same words as the frontend's
    // nightDuty dictionary.
    private sealed record FeedTexts(string FeedName, string Night, string Sleepover, string Babysitter, string FormerGuardian);

    private static readonly Dictionary<string, FeedTexts> Texts = new Dictionary<string, FeedTexts>
    {
        ["en"] = new("Night duty: {0}", "Night: {0}", "Sleepover: {0}", "Babysitter", "a former guardian"),
        ["da"] = new("Nattevagt: {0}", "Nat: {0}", "Overnatning: {0}", "Babysitter", "en tidligere voksen"),
    };

    private const string Separator = " · ";

    public static string Write(IReadOnlyCollection<Night> nights, NightFeedContext context, TimeProvider time)
    {
        var texts = Texts.GetValueOrDefault(context.Language.Value) ?? Texts[SupportedLanguages.Default.Value];

        var calendar = new IcsCalendar();
        IcalSubscription.Describe(calendar, Format(texts.FeedName, context.ChildName));

        // Start of the UTC day, not UtcNow: an unchanged feed must render byte-identically within a
        // day or its ETag never matches (see docs/backend/analysis/conditional-get-etags.md).
        var stamp = new CalDateTime(time.GetUtcNow().UtcDateTime.Date, "UTC");

        foreach (var night in nights)
        {
            // Unplanned and cleared nights have nothing to show: no event at all.
            if (night.Status is not NightStatus.NightPlanned planned)
            {
                continue;
            }

            var entry = planned.Entry;
            var sleepover = entry.Responsible as SleepoverNightResponsibleDto;

            calendar.Events.Add(new IcsEvent
            {
                Uid = $"{context.ChildId.Value:N}-{night.Date:yyyyMMdd}@buddy-night",
                Summary = Summary(entry, context, texts),
                // "" means not given; Ical.Net writes an empty line for "" but omits null.
                Location = NullIfEmpty(sleepover?.Location),
                Description = NullIfEmpty(sleepover?.ContactInfo),
                DtStamp = stamp,
                // Dates only (night-duty.md, requirement 6): the night of D is an all-day event on D.
                DtStart = new CalDateTime(night.Date),
                DtEnd = new CalDateTime(night.Date.AddDays(1)),
            });
        }

        // SerializeToString is annotated as returning string?, but never actually returns null for
        // a non-null Calendar instance.
        return new CalendarSerializer().SerializeToString(calendar)!;
    }

    // "Night: Dad · Screen night", "Sleepover: Grandma Lis", or "Night: Screen night" for a note on
    // its own.
    private static string Summary(NightEntryDto entry, NightFeedContext context, FeedTexts texts)
    {
        var note = entry.Note ?? "";

        var who = entry.Responsible switch
        {
            GuardianNightResponsibleDto guardian => Format(texts.Night,
                context.GuardianNames.GetValueOrDefault(guardian.GuardianId) ?? texts.FormerGuardian),
            BabysitterNightResponsibleDto babysitter => Format(texts.Night,
                string.IsNullOrEmpty(babysitter.Name) ? texts.Babysitter : babysitter.Name),
            SleepoverNightResponsibleDto sleepover => Format(texts.Sleepover, sleepover.HostName),
            _ => null,
        };

        return (who, note) switch
        {
            (null, _) => Format(texts.Night, note),
            (_, "") => who,
            _ => who + Separator + note,
        };
    }

    private static string Format(string template, string value) => string.Format(CultureInfo.InvariantCulture, template, value);

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
