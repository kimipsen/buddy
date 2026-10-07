using Alba;

using buddy.Common;
using buddy.Features.Users;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Users.DeleteCurrentUser;

[Collection(BuddyApiCollection.Name)]
public sealed class DeleteCurrentUserTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("DeleteCurrentUser")]
    public async Task Deleting_the_current_user_makes_them_appear_not_found_afterwards()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        await DeleteAsync(token);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url("/users/me");
            _.StatusCodeShouldBe(404);
        });
    }

    [Fact]
    public async Task A_deleted_users_still_valid_token_is_refused_everywhere()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        await DeleteAsync(token);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url("/users/me/children");
            _.StatusCodeShouldBe(403);
        });

        Assert.Equal(ProvisionedUserMiddleware.ErrorCode, response.ReadAsJson<ErrorEnvelope>().Code);

        // Repeating the deletion is refused the same way: the user no longer exists for the API.
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url("/users/me");
            _.StatusCodeShouldBe(403);
        });
    }

    [Fact]
    public async Task Deleting_the_current_user_deletes_their_keycloak_account()
    {
        var (user, token, _) = await fixture.CreateAuthenticatedUserAsync();
        Assert.True(await fixture.KeycloakUserExistsAsync(user.Username));

        await DeleteAsync(token);

        Assert.False(await fixture.KeycloakUserExistsAsync(user.Username));
    }

    private Task<IScenarioResult> DeleteAsync(string token) =>
        fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url("/users/me");
            _.StatusCodeShouldBe(204);
        });
}
