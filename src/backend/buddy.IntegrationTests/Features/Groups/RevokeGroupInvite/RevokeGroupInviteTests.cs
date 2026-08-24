using Alba;

using buddy.Features.Groups;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Groups.RevokeGroupInvite;

[Collection(BuddyApiCollection.Name)]
public sealed class RevokeGroupInviteTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("RevokeGroupInvite")]
    public async Task The_owner_can_revoke_a_pending_invite()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");
        var (invitee, _, _) = await fixture.CreateAuthenticatedUserAsync();

        var invite = await GroupTestHelpers.InviteToGroupAsync(fixture, ownerToken, groupId, invitee.Email, GroupRole.Member);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Delete.Url($"/groups/{groupId}/invites/{invite.Id}");
            _.StatusCodeShouldBe(204);
        });

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Get.Url($"/groups/{groupId}/invites");
            _.StatusCodeShouldBeOk();
        });

        Assert.Empty(response.ReadAsJson<GroupInviteResponseDto[]>());
    }

    [Fact]
    public async Task Revoking_an_unknown_invite_id_is_idempotent()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Delete.Url($"/groups/{groupId}/invites/{Guid.NewGuid()}");
            _.StatusCodeShouldBe(204);
        });
    }

    [Fact]
    public async Task A_plain_member_cannot_revoke_invites()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");
        var (invitee, _, _) = await fixture.CreateAuthenticatedUserAsync();
        var invite = await GroupTestHelpers.InviteToGroupAsync(fixture, ownerToken, groupId, invitee.Email, GroupRole.Member);

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
            _.Delete.Url($"/groups/{groupId}/invites/{invite.Id}");
            _.StatusCodeShouldBe(403);
        });
    }
}
