using Alba;

using buddy.Common;
using buddy.IntegrationTests.Features.Guardians;
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

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Contains("Name", error.Details.Keys);
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

    [Fact]
    public async Task A_meal_name_of_exactly_200_characters_is_accepted()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var name = new string('a', 200);

        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id, new CreateMealOptions(Name: name));

        Assert.NotNull(meal);
        Assert.Equal(name, meal.Name);
    }

    [Fact]
    public async Task A_meal_name_longer_than_200_characters_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new { Name = new string('a', 201), Description = (string?)null, Icon = "taco", Color = "#ffaa00" })
                .ToUrl($"/mealplans/children/{child.Id}/meals");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Contains("Name", error.Details.Keys);
    }

    [Fact]
    public async Task A_description_of_exactly_2000_characters_is_accepted()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var description = new string('d', 2000);

        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id, new CreateMealOptions(Description: description));

        Assert.NotNull(meal);
        Assert.Equal(description, meal.Description);
    }

    [Fact]
    public async Task A_description_longer_than_2000_characters_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new { Name = "Tacos", Description = new string('d', 2001), Icon = "taco", Color = "#ffaa00" })
                .ToUrl($"/mealplans/children/{child.Id}/meals");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Contains("Description", error.Details.Keys);
    }

    [Fact]
    public async Task A_group_meal_with_no_name_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var groupId = await MealplanTestHelpers.ShareWithNewGroupAsync(fixture, guardianToken, child.Id);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new { Name = " ", Description = (string?)null, Icon = "taco", Color = "#ffaa00" })
                .ToUrl($"/mealplans/groups/{groupId}/meals");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Contains("Name", error.Details.Keys);
    }

    [Fact]
    public async Task A_group_meal_with_a_200_character_name_and_a_2000_character_description_is_accepted()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var groupId = await MealplanTestHelpers.ShareWithNewGroupAsync(fixture, guardianToken, child.Id);
        var name = new string('a', 200);
        var description = new string('d', 2000);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new { Name = name, Description = description, Icon = "taco", Color = "#ffaa00" })
                .ToUrl($"/mealplans/groups/{groupId}/meals");
            _.StatusCodeShouldBeOk();
        });

        var meal = response.ReadAsJson<MealDto>();
        Assert.Equal(name, meal.Name);
        Assert.Equal(description, meal.Description);
    }

    [Fact]
    public async Task A_group_meal_name_longer_than_200_characters_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var groupId = await MealplanTestHelpers.ShareWithNewGroupAsync(fixture, guardianToken, child.Id);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new { Name = new string('a', 201), Description = (string?)null, Icon = "taco", Color = "#ffaa00" })
                .ToUrl($"/mealplans/groups/{groupId}/meals");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Contains("Name", error.Details.Keys);
    }

    [Fact]
    public async Task A_group_meal_description_longer_than_2000_characters_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var groupId = await MealplanTestHelpers.ShareWithNewGroupAsync(fixture, guardianToken, child.Id);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new { Name = "Tacos", Description = new string('d', 2001), Icon = "taco", Color = "#ffaa00" })
                .ToUrl($"/mealplans/groups/{groupId}/meals");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Contains("Description", error.Details.Keys);
    }
}
