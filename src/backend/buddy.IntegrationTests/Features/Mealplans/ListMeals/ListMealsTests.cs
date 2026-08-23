using Alba;

using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.Mealplans;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Mealplans.ListMeals;

[Collection(BuddyApiCollection.Name)]
public sealed class ListMealsTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ListMeals")]
    public async Task Lists_every_meal_created_for_the_child()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id, new CreateMealOptions(Name: "Tacos"));
        await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id, new CreateMealOptions(Name: "Pasta"));

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/mealplans/children/{child.Id}/meals");
            _.StatusCodeShouldBeOk();
        });

        var meals = response.ReadAsJson<List<MealDto>>();
        Assert.Equal(2, meals.Count);
        Assert.Contains(meals, m => m.Name == "Tacos");
        Assert.Contains(meals, m => m.Name == "Pasta");
    }

    [Fact]
    public async Task The_child_can_also_list_their_own_meals()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id, new CreateMealOptions(Name: "Tacos"));

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Get.Url($"/mealplans/children/{child.Id}/meals");
            _.StatusCodeShouldBeOk();
        });

        Assert.Single(response.ReadAsJson<List<MealDto>>());
    }

    [Fact]
    public async Task A_third_party_with_no_guardian_link_gets_not_found()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {strangerToken}");
            _.Get.Url($"/mealplans/children/{child.Id}/meals");
            _.StatusCodeShouldBe(404);
        });
    }
}
