using Alba;

using buddy.Common;
using buddy.IntegrationTests.Features.Guardians;
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

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Contains("Name", error.Details.Keys);
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

    [Fact]
    public async Task A_200_character_name_and_a_2000_character_description_are_accepted()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(meal);
        var name = new string('a', 200);
        var description = new string('d', 2000);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Patch.Json(new { Name = name, Description = description, Icon = "taco", Color = "#ffaa00" })
                .ToUrl($"/mealplans/children/{child.Id}/meals/{meal.Id}/details");
            _.StatusCodeShouldBeOk();
        });

        var updated = response.ReadAsJson<MealDto>();
        Assert.Equal(name, updated.Name);
        Assert.Equal(description, updated.Description);
    }

    [Fact]
    public async Task A_name_longer_than_200_characters_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(meal);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Patch.Json(new { Name = new string('a', 201), Description = (string?)null, Icon = "taco", Color = "#ffaa00" })
                .ToUrl($"/mealplans/children/{child.Id}/meals/{meal.Id}/details");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Contains("Name", error.Details.Keys);
    }

    [Fact]
    public async Task A_description_longer_than_2000_characters_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(meal);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Patch.Json(new { Name = "Tacos", Description = new string('d', 2001), Icon = "taco", Color = "#ffaa00" })
                .ToUrl($"/mealplans/children/{child.Id}/meals/{meal.Id}/details");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Contains("Description", error.Details.Keys);
    }

    [Fact]
    public async Task A_group_meal_update_with_no_name_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(meal);
        var groupId = await MealplanTestHelpers.ShareWithNewGroupAsync(fixture, guardianToken, child.Id);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Patch.Json(new { Name = " ", Description = (string?)null, Icon = "taco", Color = "#ffaa00" })
                .ToUrl($"/mealplans/groups/{groupId}/meals/{meal.Id}/details");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Contains("Name", error.Details.Keys);
    }

    [Fact]
    public async Task A_group_meal_update_with_a_200_character_name_and_a_2000_character_description_is_accepted()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(meal);
        var groupId = await MealplanTestHelpers.ShareWithNewGroupAsync(fixture, guardianToken, child.Id);
        var name = new string('a', 200);
        var description = new string('d', 2000);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Patch.Json(new { Name = name, Description = description, Icon = "taco", Color = "#ffaa00" })
                .ToUrl($"/mealplans/groups/{groupId}/meals/{meal.Id}/details");
            _.StatusCodeShouldBeOk();
        });

        var updated = response.ReadAsJson<MealDto>();
        Assert.Equal(name, updated.Name);
        Assert.Equal(description, updated.Description);
    }

    [Fact]
    public async Task A_group_meal_update_with_a_name_longer_than_200_characters_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(meal);
        var groupId = await MealplanTestHelpers.ShareWithNewGroupAsync(fixture, guardianToken, child.Id);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Patch.Json(new { Name = new string('a', 201), Description = (string?)null, Icon = "taco", Color = "#ffaa00" })
                .ToUrl($"/mealplans/groups/{groupId}/meals/{meal.Id}/details");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Contains("Name", error.Details.Keys);
    }

    [Fact]
    public async Task A_group_meal_update_with_a_description_longer_than_2000_characters_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(meal);
        var groupId = await MealplanTestHelpers.ShareWithNewGroupAsync(fixture, guardianToken, child.Id);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Patch.Json(new { Name = "Tacos", Description = new string('d', 2001), Icon = "taco", Color = "#ffaa00" })
                .ToUrl($"/mealplans/groups/{groupId}/meals/{meal.Id}/details");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Contains("Description", error.Details.Keys);
    }
}
