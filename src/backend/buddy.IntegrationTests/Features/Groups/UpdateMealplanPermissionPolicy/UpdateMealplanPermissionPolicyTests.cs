using Alba;

using buddy.Features.Groups;
using buddy.Features.Mealplans;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Groups.UpdateMealplanPermissionPolicy;

[Collection(BuddyApiCollection.Name)]
public sealed class UpdateMealplanPermissionPolicyTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("UpdateGroupMealplanPermissionPolicy")]
    public async Task The_owner_can_reconfigure_the_full_policy()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");

        var policy = new Dictionary<GroupRole, MealplanAccessTier>
        {
            [GroupRole.Owner] = MealplanAccessTier.Manage,
            [GroupRole.Admin] = MealplanAccessTier.Manage,
            [GroupRole.Member] = MealplanAccessTier.Manage
        };

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Put.Json(new { Policy = policy }).ToUrl($"/groups/{groupId}/mealplan-permission-policy");
            _.StatusCodeShouldBe(204);
        });

        var group = await GroupTestHelpers.GetGroupAsync(fixture, ownerToken, groupId);
        Assert.Equal(MealplanAccessTier.Manage, group.MealplanPermissionPolicy[GroupRole.Member]);
    }

    [Fact]
    public async Task The_view_tier_is_accepted_as_a_valid_policy_value()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");

        var policy = new Dictionary<GroupRole, MealplanAccessTier>
        {
            [GroupRole.Owner] = MealplanAccessTier.Manage,
            [GroupRole.Admin] = MealplanAccessTier.View,
            [GroupRole.Member] = MealplanAccessTier.View
        };

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Put.Json(new { Policy = policy }).ToUrl($"/groups/{groupId}/mealplan-permission-policy");
            _.StatusCodeShouldBe(204);
        });

        var group = await GroupTestHelpers.GetGroupAsync(fixture, ownerToken, groupId);
        Assert.Equal(MealplanAccessTier.View, group.MealplanPermissionPolicy[GroupRole.Admin]);
        Assert.Equal(MealplanAccessTier.View, group.MealplanPermissionPolicy[GroupRole.Member]);
    }

    [Fact]
    public async Task A_new_group_defaults_owner_and_admin_to_manage_and_member_to_none()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");

        var group = await GroupTestHelpers.GetGroupAsync(fixture, ownerToken, groupId);

        Assert.Equal(MealplanAccessTier.Manage, group.MealplanPermissionPolicy[GroupRole.Owner]);
        Assert.Equal(MealplanAccessTier.Manage, group.MealplanPermissionPolicy[GroupRole.Admin]);
        Assert.Equal(MealplanAccessTier.None, group.MealplanPermissionPolicy[GroupRole.Member]);
    }

    [Fact]
    public async Task A_policy_missing_a_role_is_rejected()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");

        var incompletePolicy = new Dictionary<GroupRole, MealplanAccessTier>
        {
            [GroupRole.Owner] = MealplanAccessTier.Manage,
            [GroupRole.Admin] = MealplanAccessTier.Manage
        };

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Put.Json(new { Policy = incompletePolicy }).ToUrl($"/groups/{groupId}/mealplan-permission-policy");
            _.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task A_policy_using_the_child_only_rate_tier_is_rejected()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");

        var policy = new Dictionary<GroupRole, MealplanAccessTier>
        {
            [GroupRole.Owner] = MealplanAccessTier.Manage,
            [GroupRole.Admin] = MealplanAccessTier.Manage,
            [GroupRole.Member] = MealplanAccessTier.Rate
        };

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Put.Json(new { Policy = policy }).ToUrl($"/groups/{groupId}/mealplan-permission-policy");
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

        var policy = new Dictionary<GroupRole, MealplanAccessTier>
        {
            [GroupRole.Owner] = MealplanAccessTier.Manage,
            [GroupRole.Admin] = MealplanAccessTier.Manage,
            [GroupRole.Member] = MealplanAccessTier.Manage
        };

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {memberToken}");
            _.Put.Json(new { Policy = policy }).ToUrl($"/groups/{groupId}/mealplan-permission-policy");
            _.StatusCodeShouldBe(403);
        });
    }
}
