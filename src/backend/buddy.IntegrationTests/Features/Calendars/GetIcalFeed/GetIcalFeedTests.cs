using Alba;

using buddy.IntegrationTests.Features.Calendars;
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
        });

        var ics = response.ReadAsText();
        Assert.Contains("Feed Event", ics);
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
