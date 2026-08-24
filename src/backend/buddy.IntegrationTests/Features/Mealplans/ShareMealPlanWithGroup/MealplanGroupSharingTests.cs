using Alba;

using buddy.Features.Groups;
using buddy.Features.Mealplans;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.Mealplans;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Mealplans.ShareMealPlanWithGroup;

[Collection(BuddyApiCollection.Name)]
public sealed class MealplanGroupSharingTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ShareMealPlanWithGroup")]
    [CoversEndpoint("GetSharedGroup")]
    [CoversEndpoint("ListMealPlanForGroup")]
    [CoversEndpoint("ListMealsForGroup")]
    [CoversEndpoint("CreateMealForGroup")]
    [CoversEndpoint("AssignMealToSlotForGroup")]
    [CoversEndpoint("UpdateMealDetailsForGroup")]
    [CoversEndpoint("ArchiveMealForGroup")]
    [CoversEndpoint("ClearMealSlotForGroup")]
    [CoversEndpoint("UnshareMealPlanFromGroup")]
    public async Task Sharing_with_a_group_grants_its_manage_tier_members_full_read_write_access_until_unshared()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var tacos = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id, new CreateMealOptions(Name: "Tacos"));
        Assert.NotNull(tacos);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var tomorrow = today.AddDays(1);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { MealId = tacos.Id, Notes = (string?)null })
                .ToUrl($"/mealplans/children/{child.Id}/plan")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "Dinner");
            _.StatusCodeShouldBeOk();
        });

        // The guardian is the group's Owner, so ShareMealPlanWithGroup's group-management check
        // is satisfied by the same call that created the group.
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, guardianToken, "Co-parents");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Url($"/mealplans/children/{child.Id}/plan/groups/{groupId}");
            _.StatusCodeShouldBe(204);
        });

        var sharedGroupResponse = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/mealplans/children/{child.Id}/plan/groups");
            _.StatusCodeShouldBeOk();
        });
        Assert.Equal(groupId, sharedGroupResponse.ReadAsJson<SharedGroupResponseDto>().GroupId);

        // A default-policy Admin gets Manage tier the instant they're granted the role -- no
        // separate opt-in needed.
        var (_, adminToken, adminId) = await fixture.CreateAuthenticatedUserAsync();
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { Role = GroupRole.Admin }).ToUrl($"/groups/{groupId}/members/{adminId}");
            _.StatusCodeShouldBe(204);
        });

        var listPlanResponse = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {adminToken}");
            _.Get.Url($"/mealplans/groups/{groupId}/plan?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}");
            _.StatusCodeShouldBeOk();
        });

        var entry = Assert.Single(listPlanResponse.ReadAsJson<List<MealPlanEntryDto>>());
        Assert.Equal(tacos.Id, entry.MealId);

        var pancakes = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {adminToken}");
            _.Post.Json(new { Name = "Pancakes", Description = (string?)null, Icon = "pancakes", Color = "#ffaa00" })
                .ToUrl($"/mealplans/groups/{groupId}/meals");
            _.StatusCodeShouldBeOk();
        });
        var pancakesMeal = pancakes.ReadAsJson<MealDto>();

        var listMealsResponse = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {adminToken}");
            _.Get.Url($"/mealplans/groups/{groupId}/meals");
            _.StatusCodeShouldBeOk();
        });
        Assert.Contains(listMealsResponse.ReadAsJson<List<MealDto>>(), m => m.Id == pancakesMeal.Id);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {adminToken}");
            _.Put.Json(new { MealId = pancakesMeal.Id, Notes = (string?)null })
                .ToUrl($"/mealplans/groups/{groupId}/plan")
                .QueryString("date", $"{tomorrow:yyyy-MM-dd}")
                .QueryString("slot", "Breakfast");
            _.StatusCodeShouldBeOk();
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {adminToken}");
            _.Patch.Json(new { Name = "Fluffy Pancakes", Description = (string?)null, Icon = "pancakes", Color = "#ffaa00" })
                .ToUrl($"/mealplans/groups/{groupId}/meals/{pancakesMeal.Id}/details");
            _.StatusCodeShouldBeOk();
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {adminToken}");
            _.Delete.Url($"/mealplans/groups/{groupId}/meals/{pancakesMeal.Id}");
            _.StatusCodeShouldBe(204);
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {adminToken}");
            _.Delete.Url($"/mealplans/groups/{groupId}/plan")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "Dinner");
            _.StatusCodeShouldBe(204);
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Delete.Url($"/mealplans/children/{child.Id}/plan/groups/{groupId}");
            _.StatusCodeShouldBe(204);
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {adminToken}");
            _.Get.Url($"/mealplans/groups/{groupId}/plan?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}");
            _.StatusCodeShouldBe(404);
        });

        var afterUnshareResponse = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/mealplans/children/{child.Id}/plan/groups");
            _.StatusCodeShouldBeOk();
        });
        Assert.Null(afterUnshareResponse.ReadAsJson<SharedGroupResponseDto>().GroupId);
    }

    [Fact]
    public async Task A_member_with_the_default_none_policy_cannot_reach_the_shared_plan()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, guardianToken, "Co-parents");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Url($"/mealplans/children/{child.Id}/plan/groups/{groupId}");
            _.StatusCodeShouldBe(204);
        });

        var (_, memberToken, memberId) = await fixture.CreateAuthenticatedUserAsync();
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { Role = GroupRole.Member }).ToUrl($"/groups/{groupId}/members/{memberId}");
            _.StatusCodeShouldBe(204);
        });

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {memberToken}");
            _.Get.Url($"/mealplans/groups/{groupId}/plan?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}");
            _.StatusCodeShouldBe(404);
        });
    }

    [Fact]
    public async Task A_view_tier_member_can_read_the_shared_plan_but_not_write_to_it()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var tacos = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id, new CreateMealOptions(Name: "Tacos"));
        Assert.NotNull(tacos);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { MealId = tacos.Id, Notes = (string?)null })
                .ToUrl($"/mealplans/children/{child.Id}/plan")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "Dinner");
            _.StatusCodeShouldBeOk();
        });

        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, guardianToken, "Grandparents");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Url($"/mealplans/children/{child.Id}/plan/groups/{groupId}");
            _.StatusCodeShouldBe(204);
        });

        var (_, viewerToken, viewerId) = await fixture.CreateAuthenticatedUserAsync();
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { Role = GroupRole.Member }).ToUrl($"/groups/{groupId}/members/{viewerId}");
            _.StatusCodeShouldBe(204);
        });

        var policy = new Dictionary<GroupRole, MealplanAccessTier>
        {
            [GroupRole.Owner] = MealplanAccessTier.Manage,
            [GroupRole.Admin] = MealplanAccessTier.Manage,
            [GroupRole.Member] = MealplanAccessTier.View
        };
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { Policy = policy }).ToUrl($"/groups/{groupId}/mealplan-permission-policy");
            _.StatusCodeShouldBe(204);
        });

        var listPlanResponse = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {viewerToken}");
            _.Get.Url($"/mealplans/groups/{groupId}/plan?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}");
            _.StatusCodeShouldBeOk();
        });
        Assert.Equal(tacos.Id, Assert.Single(listPlanResponse.ReadAsJson<List<MealPlanEntryDto>>()).MealId);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {viewerToken}");
            _.Get.Url($"/mealplans/groups/{groupId}/meals");
            _.StatusCodeShouldBeOk();
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {viewerToken}");
            _.Post.Json(new { Name = "Pizza", Description = (string?)null, Icon = "pizza", Color = "#ffaa00" })
                .ToUrl($"/mealplans/groups/{groupId}/meals");
            _.StatusCodeShouldBe(403);
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {viewerToken}");
            _.Put.Json(new { MealId = tacos.Id, Notes = (string?)null })
                .ToUrl($"/mealplans/groups/{groupId}/plan")
                .QueryString("date", $"{today.AddDays(1):yyyy-MM-dd}")
                .QueryString("slot", "Dinner");
            _.StatusCodeShouldBe(403);
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {viewerToken}");
            _.Delete.Url($"/mealplans/groups/{groupId}/plan")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "Dinner");
            _.StatusCodeShouldBe(403);
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {viewerToken}");
            _.Patch.Json(new { Name = "Renamed Tacos", Description = (string?)null, Icon = "taco", Color = "#ffaa00" })
                .ToUrl($"/mealplans/groups/{groupId}/meals/{tacos.Id}/details");
            _.StatusCodeShouldBe(403);
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {viewerToken}");
            _.Delete.Url($"/mealplans/groups/{groupId}/meals/{tacos.Id}");
            _.StatusCodeShouldBe(403);
        });
    }

    [Fact]
    public async Task Sharing_requires_the_guardian_to_also_manage_the_target_group()
    {
        var (_, guardianToken, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Someone else's group");

        // The guardian has full Manage tier on their own child's plan, but only a plain Member
        // role in this group -- Owner/Admin is required to share into it.
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Put.Json(new { Role = GroupRole.Member }).ToUrl($"/groups/{groupId}/members/{guardianId}");
            _.StatusCodeShouldBe(204);
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Url($"/mealplans/children/{child.Id}/plan/groups/{groupId}");
            _.StatusCodeShouldBe(403);
        });
    }
}
