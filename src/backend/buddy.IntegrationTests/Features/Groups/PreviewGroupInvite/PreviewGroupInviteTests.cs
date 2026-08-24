using Alba;

using buddy.Features.Groups;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Groups.PreviewGroupInvite;

[Collection(BuddyApiCollection.Name)]
public sealed class PreviewGroupInviteTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("PreviewGroupInvite")]
    public async Task Anyone_can_preview_an_invite_without_logging_in()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Engineering");
        var (invitee, _, _) = await fixture.CreateAuthenticatedUserAsync();

        await GroupTestHelpers.InviteToGroupAsync(fixture, ownerToken, groupId, invitee.Email, GroupRole.Member);
        var token = await GroupTestHelpers.ReadInviteTokenAsync(fixture, invitee.Email);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.Get.Url($"/invites/{token}/preview");
            _.StatusCodeShouldBeOk();
        });

        var preview = response.ReadAsJson<GroupInvitePreviewResponseDto>();
        Assert.Equal("Engineering", preview.GroupName);
    }

    [Fact]
    public async Task An_unknown_token_is_not_found()
    {
        await fixture.Host.Scenario(_ =>
        {
            _.Get.Url("/invites/not-a-real-token/preview");
            _.StatusCodeShouldBe(404);
        });
    }
}
