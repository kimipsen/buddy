using System.Text.RegularExpressions;

using Alba;

using buddy.Features.Calendars;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Calendars.GetIcalFeed;

[Collection(BuddyApiCollection.Name)]
public sealed class GetIcalFeedTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("GetCalendarIcalFeed")]
    public async Task An_anonymous_request_with_a_valid_token_returns_the_ics_feed_with_scheduled_items()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        await CalendarTestHelpers.CreateEventAsync(fixture, token, calendarId, "Feed Event");
        var issued = await CalendarTestHelpers.CreateIcalTokenAsync(fixture, token, calendarId);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.Get.Url($"/calendars/{calendarId}/ical/{issued.Token}");
            _.StatusCodeShouldBeOk();
            _.ContentTypeShouldBe("text/calendar");
            _.Header("Cache-Control").SingleValueShouldEqual("private, no-cache");
        });

        var ics = response.ReadAsText();
        Assert.Contains("Feed Event", ics);
        Assert.Contains("NAME:Personal", ics);
        Assert.Contains("X-WR-CALNAME:Personal", ics);
        Assert.Contains("REFRESH-INTERVAL;VALUE=DURATION:PT1H", ics);
        Assert.Contains("X-PUBLISHED-TTL:PT1H", ics);

        // An unchanged feed renders byte-identically (DTSTAMP is per day, not per request), so a
        // calendar app sending the ETag back gets a 304 instead of the whole feed.
        var etag = response.Context.Response.Headers.ETag.ToString();
        Assert.NotEmpty(etag);

        var revalidated = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("If-None-Match", etag);
            _.Get.Url($"/calendars/{calendarId}/ical/{issued.Token}");
            _.StatusCodeShouldBe(304);
        });
        Assert.Equal("private, no-cache", revalidated.Context.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task An_all_day_event_is_written_with_a_date_only_value()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        await CalendarTestHelpers.CreateEventAsync(fixture, token, calendarId, "All Day Feed Event", isAllDay: true);
        var issued = await CalendarTestHelpers.CreateIcalTokenAsync(fixture, token, calendarId);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.Get.Url($"/calendars/{calendarId}/ical/{issued.Token}");
            _.StatusCodeShouldBeOk();
        });

        var ics = response.ReadAsText();
        Assert.Contains("All Day Feed Event", ics);
        Assert.Contains("DTSTART;VALUE=DATE:", ics);
        Assert.Contains("DTEND;VALUE=DATE:", ics);
    }

    [Fact]
    public async Task A_task_is_written_as_a_todo_due_at_its_due_instant()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        await CalendarTestHelpers.CreateTaskAsync(fixture, token, calendarId, "Feed Task");
        var issued = await CalendarTestHelpers.CreateIcalTokenAsync(fixture, token, calendarId);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.Get.Url($"/calendars/{calendarId}/ical/{issued.Token}");
            _.StatusCodeShouldBeOk();
        });

        var ics = response.ReadAsText();
        Assert.Contains("BEGIN:VTODO", ics);
        Assert.Contains("SUMMARY:Feed Task", ics);
        Assert.Matches(@"DUE:\d{8}T\d{6}Z", ics);
        Assert.DoesNotContain("BEGIN:VEVENT", ics);
    }

    // docs/backend/analysis/skip-occurrences.md, Question 2: the feed writes one VTODO per
    // occurrence, so a skipped day simply isn't written -- no EXDATE needed.
    [Fact]
    public async Task A_skipped_occurrence_is_left_out_of_the_feed()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var task = await CalendarTestHelpers.CreateTaskAsync(
            fixture, token, calendarId, "Daily Feed Task", start,
            new RecurrenceRuleRequest(RecurrenceFrequency.Daily, 1, start.AddDays(2)));
        var issued = await CalendarTestHelpers.CreateIcalTokenAsync(fixture, token, calendarId);

        await CalendarTestHelpers.SetOccurrenceSkipAsync(fixture, token, calendarId, task!.Id, start.AddDays(1), isSkipped: true);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.Get.Url($"/calendars/{calendarId}/ical/{issued.Token}");
            _.StatusCodeShouldBeOk();
        });

        var ics = response.ReadAsText();
        Assert.Equal(2, Regex.Count(ics, "BEGIN:VTODO"));
        Assert.DoesNotContain($"DUE:{start.AddDays(1):yyyyMMdd}T", ics);
    }

    [Fact]
    public async Task An_invalid_token_returns_not_found()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");

        await fixture.Host.Scenario(_ =>
        {
            _.Get.Url($"/calendars/{calendarId}/ical/not-a-real-token");
            _.StatusCodeShouldBe(404);
        });
    }
}
