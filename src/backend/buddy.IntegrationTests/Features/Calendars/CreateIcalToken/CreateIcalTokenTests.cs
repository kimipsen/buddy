using Alba;

using buddy.Features.Calendars;
using buddy.IntegrationTests.Features.Calendars;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Calendars.CreateIcalToken;

[Collection(BuddyApiCollection.Name)]
public sealed class CreateIcalTokenTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("CreateCalendarIcalToken")]
    public async Task The_owner_can_issue_a_subscription_token()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Url($"/calendars/{calendarId}/ical-tokens");
            _.StatusCodeShouldBeOk();
        });

        var body = response.ReadAsJson<IcalTokenResponseDto>();
        Assert.NotEmpty(body.Token);
        Assert.Contains(body.Token, body.SubscriptionPath);
    }

    [Fact]
    public async Task A_contributor_cannot_issue_a_subscription_token()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, ownerToken, "Shared");
        var (_, contributorToken, contributorId) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Put.Json(new { Role = CalendarRole.Contributor }).ToUrl($"/calendars/{calendarId}/members/{contributorId}");
            _.StatusCodeShouldBe(204);
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {contributorToken}");
            _.Post.Url($"/calendars/{calendarId}/ical-tokens");
            _.StatusCodeShouldBe(403);
        });
    }
}
