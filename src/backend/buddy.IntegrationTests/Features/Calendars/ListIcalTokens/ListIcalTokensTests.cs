using Alba;

using buddy.IntegrationTests.Features.Calendars;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Calendars.ListIcalTokens;

[Collection(BuddyApiCollection.Name)]
public sealed class ListIcalTokensTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ListCalendarIcalTokens")]
    public async Task The_owner_can_list_issued_tokens_without_seeing_their_secret_value()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var issued = await CalendarTestHelpers.CreateIcalTokenAsync(fixture, token, calendarId);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"/calendars/{calendarId}/ical-tokens");
            _.StatusCodeShouldBeOk();
        });

        var tokens = response.ReadAsJson<List<IcalTokenSummaryDto>>();
        Assert.Contains(tokens, t => t.TokenId == issued.TokenId);
    }
}
