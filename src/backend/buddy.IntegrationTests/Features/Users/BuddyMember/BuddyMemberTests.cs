using Alba;

using buddy.Common;
using buddy.Features.Users;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

using Xunit;

namespace buddy.IntegrationTests.Features.Users.BuddyMember;

// A valid token for an account in neither the buddy-guardian nor the buddy-child Keycloak group is
// rejected by BuddyMemberMiddleware before provisioning or any handler runs.
[Collection(BuddyApiCollection.Name)]
public sealed class BuddyMemberTests(BuddyApiFixture fixture)
{
    [Theory]
    [InlineData("GET", "/users/me")]
    [InlineData("GET", "/users/me/children")]
    [InlineData("GET", "/groups")]
    [InlineData("POST", "/groups")]
    [InlineData("POST", "/guardian-invites/some-token/accept")]
    public async Task An_account_in_neither_group_is_forbidden_with_not_a_buddy_member(string method, string url)
    {
        var token = await TokenInGroupsAsync([]);

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
        Assert.Equal(BuddyMemberMiddleware.ErrorCode, error.Code);
        Assert.False(string.IsNullOrEmpty(error.RequestId));
    }

    [Fact]
    public async Task An_account_in_neither_group_is_not_provisioned()
    {
        var token = await TokenInGroupsAsync([]);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url("/users/me");
            _.StatusCodeShouldBe(403);
        });

        var subject = new JsonWebToken(token).Subject;
        var identity = await fixture.Host.Services.GetRequiredService<IUserEventStore>()
            .FindIdentityAsync(KeycloakSubject.New(subject), CancellationToken.None);
        Assert.Null(identity);
    }

    [Theory]
    [InlineData(KeycloakGroups.Guardian)]
    [InlineData(KeycloakGroups.Child)]
    public async Task An_account_in_either_group_can_use_Buddy(string group)
    {
        var token = await TokenInGroupsAsync([group]);

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
    public async Task An_anonymous_endpoint_ignores_a_token_in_neither_group()
    {
        var token = await TokenInGroupsAsync([]);

        // The invite preview is AllowAnonymous: an unknown token is its own 404, not a 403.
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url("/invites/some-token/preview");
            _.StatusCodeShouldBe(404);
        });
    }

    private async Task<string> TokenInGroupsAsync(string[] groups)
    {
        var user = await fixture.CreateUserAsync(groups: groups);
        return await fixture.GetAccessTokenAsync(user);
    }
}
