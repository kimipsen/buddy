using Alba;

using buddy.Features.Groups;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Groups.DeleteGroup;

[Collection(BuddyApiCollection.Name)]
public sealed class DeleteGroupTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("DeleteGroup")]
    public async Task The_owner_can_delete_the_group_and_it_then_disappears_for_everyone()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Delete.Url($"/groups/{groupId}");
            _.StatusCodeShouldBe(204);
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Get.Url($"/groups/{groupId}");
            _.StatusCodeShouldBe(404);
        });
    }

    [Fact]
    public async Task An_admin_who_is_not_the_owner_cannot_delete_the_group()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");
        var (_, adminToken, adminId) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Put.Json(new { Role = GroupRole.Admin }).ToUrl($"/groups/{groupId}/members/{adminId}");
            _.StatusCodeShouldBe(204);
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {adminToken}");
            _.Delete.Url($"/groups/{groupId}");
            _.StatusCodeShouldBe(403);
        });
    }
}
