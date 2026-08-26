using Alba;

using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.Mealplans;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Mealplans.RevokeMealPlanIcalToken;

[Collection(BuddyApiCollection.Name)]
public sealed class RevokeMealPlanIcalTokenTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("RevokeMealPlanIcalToken")]
    public async Task Revoking_a_token_makes_its_feed_stop_working()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var issued = await MealplanTestHelpers.CreateIcalTokenAsync(fixture, guardianToken, child.Id);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Delete.Url($"/mealplans/children/{child.Id}/ical-tokens/{issued.TokenId}");
            _.StatusCodeShouldBe(204);
        });

        await fixture.Host.Scenario(_ =>
        {
            _.Get.Url(issued.SubscriptionPath);
            _.StatusCodeShouldBe(404);
        });
    }
}
