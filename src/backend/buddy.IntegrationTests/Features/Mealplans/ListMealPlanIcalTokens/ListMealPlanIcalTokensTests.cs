using Alba;

using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.Mealplans;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Mealplans.ListMealPlanIcalTokens;

[Collection(BuddyApiCollection.Name)]
public sealed class ListMealPlanIcalTokensTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ListMealPlanIcalTokens")]
    public async Task A_guardian_can_list_issued_tokens_without_seeing_their_secret_value()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var issued = await MealplanTestHelpers.CreateIcalTokenAsync(fixture, guardianToken, child.Id);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/mealplans/children/{child.Id}/ical-tokens");
            _.StatusCodeShouldBeOk();
        });

        var tokens = response.ReadAsJson<List<MealPlanIcalTokenSummaryDto>>();
        Assert.Contains(tokens, t => t.TokenId == issued.TokenId);
    }
}
