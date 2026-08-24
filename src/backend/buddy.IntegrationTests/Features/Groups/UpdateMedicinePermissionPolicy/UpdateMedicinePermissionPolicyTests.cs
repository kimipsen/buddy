using Alba;

using buddy.Features.Groups;
using buddy.Features.Medicines;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Groups.UpdateMedicinePermissionPolicy;

[Collection(BuddyApiCollection.Name)]
public sealed class UpdateMedicinePermissionPolicyTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("UpdateGroupMedicinePermissionPolicy")]
    public async Task The_owner_can_reconfigure_the_full_policy()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");

        var policy = new Dictionary<GroupRole, MedicineAccessTier>
        {
            [GroupRole.Owner] = MedicineAccessTier.Manage,
            [GroupRole.Admin] = MedicineAccessTier.Manage,
            [GroupRole.Member] = MedicineAccessTier.Manage
        };

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Put.Json(new { Policy = policy }).ToUrl($"/groups/{groupId}/medicine-permission-policy");
            _.StatusCodeShouldBe(204);
        });

        var group = await GroupTestHelpers.GetGroupAsync(fixture, ownerToken, groupId);
        Assert.Equal(MedicineAccessTier.Manage, group.MedicinePermissionPolicy[GroupRole.Member]);
    }

    [Fact]
    public async Task A_new_group_defaults_owner_and_admin_to_manage_and_member_to_none()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");

        var group = await GroupTestHelpers.GetGroupAsync(fixture, ownerToken, groupId);

        Assert.Equal(MedicineAccessTier.Manage, group.MedicinePermissionPolicy[GroupRole.Owner]);
        Assert.Equal(MedicineAccessTier.Manage, group.MedicinePermissionPolicy[GroupRole.Admin]);
        Assert.Equal(MedicineAccessTier.None, group.MedicinePermissionPolicy[GroupRole.Member]);
    }

    [Fact]
    public async Task A_policy_missing_a_role_is_rejected()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");

        var incompletePolicy = new Dictionary<GroupRole, MedicineAccessTier>
        {
            [GroupRole.Owner] = MedicineAccessTier.Manage,
            [GroupRole.Admin] = MedicineAccessTier.Manage
        };

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Put.Json(new { Policy = incompletePolicy }).ToUrl($"/groups/{groupId}/medicine-permission-policy");
            _.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task A_policy_using_the_two_principal_mark_tier_is_rejected()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");

        var policy = new Dictionary<GroupRole, MedicineAccessTier>
        {
            [GroupRole.Owner] = MedicineAccessTier.Manage,
            [GroupRole.Admin] = MedicineAccessTier.Manage,
            [GroupRole.Member] = MedicineAccessTier.Mark
        };

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Put.Json(new { Policy = policy }).ToUrl($"/groups/{groupId}/medicine-permission-policy");
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

        var policy = new Dictionary<GroupRole, MedicineAccessTier>
        {
            [GroupRole.Owner] = MedicineAccessTier.Manage,
            [GroupRole.Admin] = MedicineAccessTier.Manage,
            [GroupRole.Member] = MedicineAccessTier.Manage
        };

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {memberToken}");
            _.Put.Json(new { Policy = policy }).ToUrl($"/groups/{groupId}/medicine-permission-policy");
            _.StatusCodeShouldBe(403);
        });
    }
}
