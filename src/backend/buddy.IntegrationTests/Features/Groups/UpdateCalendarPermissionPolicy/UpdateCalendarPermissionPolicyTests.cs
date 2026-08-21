using Alba;

using buddy.Features.Calendars;
using buddy.Features.Groups;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Groups.UpdateCalendarPermissionPolicy;

[Collection(BuddyApiCollection.Name)]
public sealed class UpdateCalendarPermissionPolicyTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("UpdateGroupCalendarPermissionPolicy")]
    public async Task The_owner_can_reconfigure_the_full_policy_including_their_own_row()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");

        var policy = new Dictionary<GroupRole, CalendarRole>
        {
            [GroupRole.Owner] = CalendarRole.Contributor,
            [GroupRole.Admin] = CalendarRole.Contributor,
            [GroupRole.Member] = CalendarRole.Viewer
        };

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Put.Json(new { Policy = policy }).ToUrl($"/groups/{groupId}/calendar-permission-policy");
            _.StatusCodeShouldBe(204);
        });

        var group = await GroupTestHelpers.GetGroupAsync(fixture, ownerToken, groupId);
        Assert.Equal(CalendarRole.Contributor, group.CalendarPermissionPolicy[GroupRole.Owner]);
    }

    [Fact]
    public async Task A_policy_missing_a_role_is_rejected()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");

        var incompletePolicy = new Dictionary<GroupRole, CalendarRole>
        {
            [GroupRole.Owner] = CalendarRole.Owner,
            [GroupRole.Admin] = CalendarRole.Contributor
        };

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Put.Json(new { Policy = incompletePolicy }).ToUrl($"/groups/{groupId}/calendar-permission-policy");
            _.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task A_plain_member_cannot_update_the_policy()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");
        var (_, memberToken, memberId) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Put.Json(new { Role = GroupRole.Member }).ToUrl($"/groups/{groupId}/members/{memberId}");
            _.StatusCodeShouldBe(204);
        });

        var policy = new Dictionary<GroupRole, CalendarRole>
        {
            [GroupRole.Owner] = CalendarRole.Owner,
            [GroupRole.Admin] = CalendarRole.Contributor,
            [GroupRole.Member] = CalendarRole.Viewer
        };

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {memberToken}");
            _.Put.Json(new { Policy = policy }).ToUrl($"/groups/{groupId}/calendar-permission-policy");
            _.StatusCodeShouldBe(403);
        });
    }
}
