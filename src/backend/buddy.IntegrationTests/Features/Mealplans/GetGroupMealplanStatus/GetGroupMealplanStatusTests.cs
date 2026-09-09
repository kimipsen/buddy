using Alba;

using buddy.Features.Mealplans;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Mealplans.GetGroupMealplanStatus;

[Collection(BuddyApiCollection.Name)]
public sealed class GetGroupMealplanStatusTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("GetGroupMealplanStatus")]
    public async Task Reports_false_until_a_plan_is_shared_with_the_group_then_true()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, guardianToken, "Co-parents");

        var beforeSharing = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/mealplans/groups/{groupId}/status");
            _.StatusCodeShouldBeOk();
        });
        Assert.False(beforeSharing.ReadAsJson<GroupMealplanStatusResponse>().HasSharedPlan);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Url($"/mealplans/children/{child.Id}/plan/groups/{groupId}");
            _.StatusCodeShouldBe(204);
        });

        var afterSharing = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/mealplans/groups/{groupId}/status");
            _.StatusCodeShouldBeOk();
        });
        Assert.True(afterSharing.ReadAsJson<GroupMealplanStatusResponse>().HasSharedPlan);
    }

    [Fact]
    public async Task A_caller_with_no_relationship_to_the_group_gets_a_false_status_not_an_error()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Someone else's group");

        var (_, outsiderToken, _) = await fixture.CreateAuthenticatedUserAsync();

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {outsiderToken}");
            _.Get.Url($"/mealplans/groups/{groupId}/status");
            _.StatusCodeShouldBeOk();
        });
        Assert.False(response.ReadAsJson<GroupMealplanStatusResponse>().HasSharedPlan);
    }
}
