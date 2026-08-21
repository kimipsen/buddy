using Alba;

using buddy.IntegrationTests.Features.Calendars;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Calendars.RevokeIcalToken;

[Collection(BuddyApiCollection.Name)]
public sealed class RevokeIcalTokenTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("RevokeCalendarIcalToken")]
    public async Task Revoking_a_token_makes_its_feed_stop_working()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var issued = await CalendarTestHelpers.CreateIcalTokenAsync(fixture, token, calendarId);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url($"/calendars/{calendarId}/ical-tokens/{issued.TokenId}");
            _.StatusCodeShouldBe(204);
        });

        await fixture.Host.Scenario(_ =>
        {
            _.Get.Url($"/calendars/{calendarId}/ical/{issued.Token}");
            _.StatusCodeShouldBe(404);
        });
    }
}
