using Alba;

using buddy.Features.Groups;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Groups.RemoveGroupMember;

[Collection(BuddyApiCollection.Name)]
public sealed class RemoveGroupMemberTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("RemoveGroupMember")]
    public async Task The_owner_can_remove_an_existing_member()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");
        var (_, _, memberId) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Put.Json(new { Role = GroupRole.Member }).ToUrl($"/groups/{groupId}/members/{memberId}");
            _.StatusCodeShouldBe(204);
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Delete.Url($"/groups/{groupId}/members/{memberId}");
            _.StatusCodeShouldBe(204);
        });

        var group = await GroupTestHelpers.GetGroupAsync(fixture, ownerToken, groupId);
        Assert.DoesNotContain(group.Members, m => m.UserId == memberId);
    }

    [Fact]
    public async Task Removing_someone_who_is_not_a_member_is_a_no_op_success()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");
        var (_, _, notAMemberId) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Delete.Url($"/groups/{groupId}/members/{notAMemberId}");
            _.StatusCodeShouldBe(204);
        });
    }

    [Fact]
    public async Task The_owner_cannot_remove_themselves()
    {
        var (_, ownerToken, ownerId) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Delete.Url($"/groups/{groupId}/members/{ownerId}");
            _.StatusCodeShouldBe(403);
        });
    }
}
