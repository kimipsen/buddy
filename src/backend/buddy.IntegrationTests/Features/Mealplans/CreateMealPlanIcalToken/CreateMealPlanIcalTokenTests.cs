using Alba;

using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.Mealplans;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Mealplans.CreateMealPlanIcalToken;

[Collection(BuddyApiCollection.Name)]
public sealed class CreateMealPlanIcalTokenTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("CreateMealPlanIcalToken")]
    public async Task A_guardian_can_issue_a_subscription_token()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        var issued = await MealplanTestHelpers.CreateIcalTokenAsync(fixture, guardianToken, child.Id);

        Assert.NotEmpty(issued.Token);
        Assert.Contains(issued.Token, issued.SubscriptionPath);
    }

    [Fact]
    public async Task Issuing_a_token_for_a_family_with_no_plan_yet_still_succeeds()
    {
        // No meal, no assignment -- the plan stream doesn't exist yet, so this exercises the lazy
        // MealPlanCreated bundling (mirrors AssignMealToSlotHandler's lazy-create path).
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        var issued = await MealplanTestHelpers.CreateIcalTokenAsync(fixture, guardianToken, child.Id);

        await fixture.Host.Scenario(_ =>
        {
            _.Get.Url(issued.SubscriptionPath);
            _.StatusCodeShouldBeOk();
            _.ContentTypeShouldBe("text/calendar");
        });
    }

    [Fact]
    public async Task The_child_cannot_issue_a_subscription_token()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Post.Url($"/mealplans/children/{child.Id}/ical-tokens");
            _.StatusCodeShouldBe(403);
        });
    }
}
