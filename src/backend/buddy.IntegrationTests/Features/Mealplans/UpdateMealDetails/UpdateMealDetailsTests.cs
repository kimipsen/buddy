using Alba;

using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.Mealplans;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Mealplans.UpdateMealDetails;

[Collection(BuddyApiCollection.Name)]
public sealed class UpdateMealDetailsTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("UpdateMealDetails")]
    public async Task A_guardian_can_update_a_meals_name_description_icon_and_color()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(meal);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Patch.Json(new { Name = "Tacos (renamed)", Description = "New recipe", Icon = "burrito", Color = "#00aaff" })
                .ToUrl($"/mealplans/children/{child.Id}/meals/{meal.Id}/details");
            _.StatusCodeShouldBeOk();
        });

        var updated = response.ReadAsJson<MealDto>();
        Assert.Equal("Tacos (renamed)", updated.Name);
        Assert.Equal("New recipe", updated.Description);
        Assert.Equal("burrito", updated.Icon);
        Assert.Equal("#00aaff", updated.Color);
    }

    [Fact]
    public async Task Updating_with_unchanged_values_is_a_no_op_success()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(meal);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Patch.Json(new { meal.Name, meal.Description, Icon = meal.Icon, Color = meal.Color })
                .ToUrl($"/mealplans/children/{child.Id}/meals/{meal.Id}/details");
            _.StatusCodeShouldBeOk();
        });

        var updated = response.ReadAsJson<MealDto>();
        Assert.Equal(meal.Name, updated.Name);
        Assert.Equal(meal.Description, updated.Description);
    }

    [Fact]
    public async Task A_meal_with_no_name_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(meal);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Patch.Json(new { Name = " ", Description = (string?)null, Icon = "taco", Color = "#ffaa00" })
                .ToUrl($"/mealplans/children/{child.Id}/meals/{meal.Id}/details");
            _.StatusCodeShouldBe(400);
        });

        Assert.Equal("A meal requires a name.", response.ReadAsJson<string>());
    }

    [Fact]
    public async Task The_child_cannot_edit_their_own_meal()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(meal);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Patch.Json(new { Name = "Hacked", Description = "", Icon = "taco", Color = "#ffaa00" })
                .ToUrl($"/mealplans/children/{child.Id}/meals/{meal.Id}/details");
            _.StatusCodeShouldBe(403);
        });
    }
}
