using Alba;

using buddy.Features.Groups;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Groups.AcceptGroupInvite;

[Collection(BuddyApiCollection.Name)]
public sealed class AcceptGroupInviteTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("AcceptGroupInvite")]
    public async Task The_invited_guardian_can_accept_and_becomes_a_member()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");
        var (invitee, inviteeToken, inviteeId) = await fixture.CreateAuthenticatedUserAsync();

        await GroupTestHelpers.InviteToGroupAsync(fixture, ownerToken, groupId, invitee.Email, GroupRole.Admin);
        var token = await GroupTestHelpers.ReadInviteTokenAsync(fixture, invitee.Email);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {inviteeToken}");
            _.Post.Url($"/invites/{token}/accept");
            _.StatusCodeShouldBe(204);
        });

        var group = await GroupTestHelpers.GetGroupAsync(fixture, ownerToken, groupId);
        var member = Assert.Single(group.Members, m => m.UserId == inviteeId);
        Assert.Equal(GroupRole.Admin, member.Role);
    }

    [Fact]
    public async Task A_different_logged_in_user_cannot_accept_someone_elses_invite()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");
        var (invitee, _, _) = await fixture.CreateAuthenticatedUserAsync();

        await GroupTestHelpers.InviteToGroupAsync(fixture, ownerToken, groupId, invitee.Email, GroupRole.Member);
        var token = await GroupTestHelpers.ReadInviteTokenAsync(fixture, invitee.Email);

        var (_, someoneElseToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {someoneElseToken}");
            _.Post.Url($"/invites/{token}/accept");
            _.StatusCodeShouldBe(403);
        });
    }

    [Fact]
    public async Task An_unknown_token_is_not_found()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Url("/invites/not-a-real-token/accept");
            _.StatusCodeShouldBe(404);
        });
    }

    [Fact]
    public async Task A_revoked_invite_cannot_be_accepted()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");
        var (invitee, inviteeToken, _) = await fixture.CreateAuthenticatedUserAsync();

        var invite = await GroupTestHelpers.InviteToGroupAsync(fixture, ownerToken, groupId, invitee.Email, GroupRole.Member);
        var token = await GroupTestHelpers.ReadInviteTokenAsync(fixture, invitee.Email);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Delete.Url($"/groups/{groupId}/invites/{invite.Id}");
            _.StatusCodeShouldBe(204);
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {inviteeToken}");
            _.Post.Url($"/invites/{token}/accept");
            _.StatusCodeShouldBe(404);
        });
    }
}
