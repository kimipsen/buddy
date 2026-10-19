using buddy.Common;
using buddy.Features.Groups;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Groups.SetGroupMemberRole;

[Collection(BuddyApiCollection.Name)]
public sealed class SetGroupMemberRoleTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("SetGroupMemberRole")]
    public async Task The_owner_can_grant_a_role_to_a_new_member()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");

        var (_, _, newMemberId) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Put.Json(new { Role = GroupRole.Admin }).ToUrl($"/groups/{groupId}/members/{newMemberId}");
            _.StatusCodeShouldBe(204);
        });

        var group = await GroupTestHelpers.GetGroupAsync(fixture, ownerToken, groupId);
        var grantedMember = Assert.Single(group.Members, m => m.UserId == newMemberId);
        Assert.Equal(GroupRole.Admin, grantedMember.Role);
    }

    [Theory]
    [InlineData(GroupRole.Owner)]
    [InlineData((GroupRole)99)]
    public async Task Granting_ownership_or_an_undefined_role_is_rejected(GroupRole role)
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");
        var (_, _, otherId) = await fixture.CreateAuthenticatedUserAsync();

        // Sent as a number: the binder accepts an enum that way, so an out-of-range one reaches the validator.
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Put.Json(new { Role = (int)role }).ToUrl($"/groups/{groupId}/members/{otherId}");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Equal("role", Assert.Single(error.Details.Keys));
    }

    [Fact]
    public async Task The_owner_cannot_change_their_own_role()
    {
        var (_, ownerToken, ownerId) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Put.Json(new { Role = GroupRole.Member }).ToUrl($"/groups/{groupId}/members/{ownerId}");
            _.StatusCodeShouldBe(403);
        });
    }

    [Fact]
    public async Task A_plain_member_cannot_grant_roles_to_others()
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

        var (_, _, targetId) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {plainMemberToken}");
            _.Put.Json(new { Role = GroupRole.Admin }).ToUrl($"/groups/{groupId}/members/{targetId}");
            _.StatusCodeShouldBe(403);
        });
    }

    [Fact]
    public async Task A_non_member_gets_not_found()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");

        var (_, outsiderToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var (_, _, targetId) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {outsiderToken}");
            _.Put.Json(new { Role = GroupRole.Member }).ToUrl($"/groups/{groupId}/members/{targetId}");
            _.StatusCodeShouldBe(404);
        });
    }
}
