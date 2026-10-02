using Alba;

using buddy.Common;
using buddy.Features.Users;
using buddy.IntegrationTests.Fixtures;

using Xunit;

namespace buddy.IntegrationTests.Features.Users.ProvisionedUser;

// A valid token whose Keycloak subject has no Buddy user yet (no GET /users/me) is rejected once,
// by ProvisionedUserMiddleware, before any handler runs -- see docs/backend/analysis/eliminate-nulls.md,
// Phase 1. One route per authorized feature group, plus the endpoints mapped outside them.
[Collection(BuddyApiCollection.Name)]
public sealed class ProvisionedUserTests(BuddyApiFixture fixture)
{
    private const string AnyId = "0191e3a0-0000-7000-8000-000000000001";

    [Theory]
    [InlineData("GET", "/users/me/events")]
    [InlineData("GET", "/users/me/children")]
    [InlineData("GET", "/users/me/guardians")]
    [InlineData("GET", "/users/me/siblings")]
    [InlineData("GET", "/groups")]
    [InlineData("POST", "/groups")]
    [InlineData("GET", $"/task-templates/children/{AnyId}")]
    [InlineData("GET", $"/calendars/{AnyId}/items")]
    [InlineData("GET", $"/medicines/groups/{AnyId}/children/{AnyId}/schedules")]
    [InlineData("GET", $"/mealplans/children/{AnyId}/plan/groups")]
    [InlineData("GET", $"/pickups/children/{AnyId}/schedule")]
    [InlineData("GET", $"/work-locations/guardians/{AnyId}")]
    [InlineData("GET", "/print-templates")]
    [InlineData("GET", "/progress/me")]
    [InlineData("POST", "/invites/some-token/accept")]
    [InlineData("POST", "/guardian-invites/some-token/accept")]
    public async Task An_unprovisioned_caller_is_forbidden_with_user_not_provisioned(string method, string url)
    {
        var token = await UnprovisionedTokenAsync();

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            if (method == "POST")
            {
                _.Post.Json(new { }).ToUrl(url);
            }
            else
            {
                _.Get.Url(url);
            }
            _.StatusCodeShouldBe(403);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal(ProvisionedUserMiddleware.ErrorCode, error.Code);
        Assert.False(string.IsNullOrEmpty(error.RequestId));
    }

    [Fact]
    public async Task Get_current_user_provisions_the_caller_who_can_then_use_other_routes()
    {
        var token = await UnprovisionedTokenAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url("/users/me");
            _.StatusCodeShouldBeOk();
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url("/groups");
            _.StatusCodeShouldBeOk();
        });
    }

    [Fact]
    public async Task An_anonymous_endpoint_ignores_an_unprovisioned_token()
    {
        var token = await UnprovisionedTokenAsync();

        // The invite preview is AllowAnonymous: an unknown token is its own 404, not a 403.
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url("/invites/some-token/preview");
            _.StatusCodeShouldBe(404);
        });
    }

    [Fact]
    public async Task A_request_without_a_token_is_still_unauthorized()
    {
        await fixture.Host.Scenario(_ =>
        {
            _.Get.Url("/groups");
            _.StatusCodeShouldBe(401);
        });
    }

    private async Task<string> UnprovisionedTokenAsync()
    {
        var user = await fixture.CreateUserAsync();
        return await fixture.GetAccessTokenAsync(user);
    }
}
