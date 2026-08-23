using Alba;

using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.Mealplans;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Mealplans.CreateMeal;

[Collection(BuddyApiCollection.Name)]
public sealed class CreateMealTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("CreateMeal")]
    public async Task A_guardian_can_create_a_meal_for_their_child()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id);

        Assert.NotNull(meal);
        Assert.Equal("Tacos", meal.Name);
        Assert.False(meal.IsArchived);
        Assert.Empty(meal.Ratings);
    }

    [Fact]
    public async Task A_meal_with_no_name_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new { Name = " ", Description = (string?)null, Icon = "taco", Color = "#ffaa00" })
                .ToUrl($"/mealplans/children/{child.Id}/meals");
            _.StatusCodeShouldBe(400);
        });

        Assert.Equal("A meal requires a name.", response.ReadAsJson<string>());
    }

    [Fact]
    public async Task The_child_cannot_create_their_own_meal()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        await MealplanTestHelpers.CreateMealAsync(fixture, childToken, child.Id, expectedStatus: 403);
    }

    [Fact]
    public async Task A_third_party_with_no_guardian_link_gets_not_found()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await MealplanTestHelpers.CreateMealAsync(fixture, strangerToken, child.Id, expectedStatus: 404);
    }
}
