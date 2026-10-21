using Alba;

using buddy.Features.Mealplans;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Xunit;

namespace buddy.IntegrationTests.Features.Mealplans.Mealie;

// The two per-user limits on the routes that reach a family's Mealie
// (docs/backend/analysis/rate-limiting.md): recipe photos have a budget of their own, so a page of
// photos never blocks a search, and the other calls can't starve the photos either.
[Collection(BuddyApiCollection.Name)]
public sealed class MealieRateLimitTests(BuddyApiFixture fixture)
{
    private Task<IAlbaHost> HostAsync() =>
        fixture.SharedHostAsync(
            nameof(MealieRateLimitTests),
            new Dictionary<string, string?>
            {
                ["RateLimiting:Mealie:PermitLimit"] = "2",
                ["RateLimiting:Mealie:Window"] = "01:00:00",
                ["RateLimiting:MealieImages:PermitLimit"] = "3",
                ["RateLimiting:MealieImages:Window"] = "01:00:00",
            },
            services => services.Replace(ServiceDescriptor.Singleton<IMealieClient>(FakeMealieClient.Instance)));

    [Fact]
    public async Task Recipe_photos_and_other_Mealie_calls_have_separate_budgets()
    {
        var family = await FamilyAsync();
        var recipe = family.Server.AddRecipe("Lasagne al forno");
        var image = $"mealie/recipes/{recipe.Id}/image";

        // Connecting used one of the two general permits.
        await family.GetAsync("mealie/recipes");
        await family.GetAsync("mealie/recipes", expectedStatus: 429);

        for (var i = 0; i < 3; i++)
        {
            await GetImageAsync(family, image, 200);
        }

        await GetImageAsync(family, image, 429);
    }

    // The photo is a file, not JSON, so it goes around MealieFamily.GetAsync.
    private static Task<IScenarioResult> GetImageAsync(MealieFamily family, string relative, int status) =>
        family.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {family.Token}");
            _.Get.Url(family.Path(relative));
            _.StatusCodeShouldBe(status);
        });

    private async Task<MealieFamily> FamilyAsync()
    {
        var host = await HostAsync();
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Emil");
        var family = new MealieFamily(host, token, guardianId, child, FakeMealieClient.Instance.NewServer());
        await family.ConnectAsync();

        return family;
    }
}
