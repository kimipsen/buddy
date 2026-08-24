using Alba;

using buddy.Features.Groups;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Groups.InviteToGroup;

[Collection(BuddyApiCollection.Name)]
public sealed class InviteToGroupTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("InviteToGroup")]
    public async Task The_owner_can_invite_a_guardian_by_email()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");
        var (invitee, _, _) = await fixture.CreateAuthenticatedUserAsync();

        var invite = await GroupTestHelpers.InviteToGroupAsync(fixture, ownerToken, groupId, invitee.Email, GroupRole.Member);

        Assert.Equal(invitee.Email.ToLowerInvariant(), invite.Email);
        Assert.Equal(GroupRole.Member, invite.Role);
    }

    [Fact]
    public async Task Inviting_as_owner_is_rejected()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");
        var (invitee, _, _) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Post.Json(new { Email = invitee.Email, Role = GroupRole.Owner }).ToUrl($"/groups/{groupId}/invites");
            _.StatusCodeShouldBe(403);
        });
    }

    [Fact]
    public async Task A_plain_member_cannot_invite_others()
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

        var (invitee, _, _) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {plainMemberToken}");
            _.Post.Json(new { Email = invitee.Email, Role = GroupRole.Member }).ToUrl($"/groups/{groupId}/invites");
            _.StatusCodeShouldBe(403);
        });
    }

    [Fact]
    public async Task A_non_member_gets_not_found()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");

        var (_, outsiderToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var (invitee, _, _) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {outsiderToken}");
            _.Post.Json(new { Email = invitee.Email, Role = GroupRole.Member }).ToUrl($"/groups/{groupId}/invites");
            _.StatusCodeShouldBe(404);
        });
    }

    [Fact]
    public async Task Re_inviting_the_same_email_immediately_is_rejected_by_the_cooldown()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");
        var (invitee, _, _) = await fixture.CreateAuthenticatedUserAsync();

        await GroupTestHelpers.InviteToGroupAsync(fixture, ownerToken, groupId, invitee.Email, GroupRole.Member);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Post.Json(new { Email = invitee.Email, Role = GroupRole.Member }).ToUrl($"/groups/{groupId}/invites");
            _.StatusCodeShouldBe(400);
        });
    }
}
