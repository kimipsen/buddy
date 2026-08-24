using Alba;

using buddy.Features.Groups;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Groups.ListGroupInvites;

[Collection(BuddyApiCollection.Name)]
public sealed class ListGroupInvitesTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ListGroupInvites")]
    public async Task The_owner_can_list_pending_invites()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");
        var (invitee, _, _) = await fixture.CreateAuthenticatedUserAsync();

        await GroupTestHelpers.InviteToGroupAsync(fixture, ownerToken, groupId, invitee.Email, GroupRole.Member);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Get.Url($"/groups/{groupId}/invites");
            _.StatusCodeShouldBeOk();
        });

        var invites = response.ReadAsJson<GroupInviteResponseDto[]>();
        var pending = Assert.Single(invites);
        Assert.Equal(invitee.Email.ToLowerInvariant(), pending.Email);
    }

    [Fact]
    public async Task A_plain_member_cannot_list_invites()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");

        var (_, plainMemberToken, plainMemberId) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Put.Json(new { Role = GroupRole.Member }).ToUrl($"/groups/{groupId}/members/{plainMemberId}");
            _.StatusCodeShouldBe(204);
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {plainMemberToken}");
            _.Get.Url($"/groups/{groupId}/invites");
            _.StatusCodeShouldBe(403);
        });
    }
}
