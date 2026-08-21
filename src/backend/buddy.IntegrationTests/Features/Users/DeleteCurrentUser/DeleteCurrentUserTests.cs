using Alba;

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

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url("/users/me");
            _.StatusCodeShouldBe(204);
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url("/users/me");
            _.StatusCodeShouldBe(404);
        });
    }
}
