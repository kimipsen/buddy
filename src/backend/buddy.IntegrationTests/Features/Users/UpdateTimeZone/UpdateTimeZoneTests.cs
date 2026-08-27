using Alba;

using buddy.Common;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Users.UpdateTimeZone;

[Collection(BuddyApiCollection.Name)]
public sealed class UpdateTimeZoneTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("UpdateCurrentTimeZone")]
    public async Task Updates_the_calling_users_time_zone()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { TimeZoneId = "Europe/Copenhagen" }).ToUrl("/users/me/timezone");
            _.StatusCodeShouldBeOk();
        });

        var body = response.ReadAsJson<TimeZoneResponseEnvelope>();

        Assert.Equal("Europe/Copenhagen", body.TimeZoneId);
    }

    [Fact]
    public async Task Rejects_an_unrecognized_time_zone()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { TimeZoneId = "Not/A_Zone" }).ToUrl("/users/me/timezone");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Contains("TimeZoneId", error.Details.Keys);
    }

    [Fact]
    public async Task Returns_not_found_when_the_caller_has_no_buddy_user_yet()
    {
        var user = await fixture.CreateUserAsync();
        var token = await fixture.GetAccessTokenAsync(user);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { TimeZoneId = "Europe/Copenhagen" }).ToUrl("/users/me/timezone");
            _.StatusCodeShouldBe(404);
        });
    }

    private sealed record TimeZoneResponseEnvelope(string TimeZoneId);
}
