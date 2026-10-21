using System.Text.Json;

using Alba;

using buddy.Features.Mealplans;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Mealplans.Mealie;

// docs/backend/analysis/mealie-integration.md, Question 4.
[Collection(BuddyApiCollection.Name)]
public sealed class MealieRecipeTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("SearchMealieRecipes")]
    public async Task Searching_lists_recipes_and_marks_the_ones_already_linked()
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture);
        var lasagne = family.Server.AddRecipe("Lasagne al forno");
        family.Server.AddRecipe("Chicken tacos", image: null);
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, family.Token, family.ChildId, new CreateMealOptions("Lasagne"));
        await family.PutAsync($"meals/{meal!.Id}/recipe", new { RecipeId = lasagne.Id });

        var all = await family.GetAsync("mealie/recipes");
        var found = await family.GetAsync("mealie/recipes?search=lasagne");

        Assert.Equal(2, all.GetProperty("total").GetInt32());
        Assert.False(all.GetProperty("items")[0].GetProperty("hasImage").GetBoolean());
        var item = Assert.Single(found.GetProperty("items").EnumerateArray());
        Assert.Equal(lasagne.Id, item.GetProperty("id").GetGuid());
        Assert.True(item.GetProperty("hasImage").GetBoolean());
        Assert.Equal(meal.Id, item.GetProperty("linkedMealId").GetGuid());
        Assert.Equal("lasagne", family.Server.Searches[^1]);
    }

    [Fact]
    public async Task A_recipe_whose_only_linked_meal_is_archived_shows_as_not_linked()
    {
        // The archived meal is gone from the library, so the guardian should add the recipe again
        // rather than be told it's already there.
        var family = await MealieTestHost.CreateFamilyAsync(fixture);
        var lasagne = family.Server.AddRecipe("Lasagne al forno");
        var archived = await MealplanTestHelpers.CreateMealAsync(fixture, family.Token, family.ChildId, new CreateMealOptions("Lasagne"));
        await family.PutAsync($"meals/{archived!.Id}/recipe", new { RecipeId = lasagne.Id });
        await family.DeleteAsync($"meals/{archived.Id}");

        var found = await family.GetAsync("mealie/recipes?search=lasagne");
        var details = await family.GetAsync($"mealie/recipes/{lasagne.Id}");

        Assert.Equal(JsonValueKind.Null, Assert.Single(found.GetProperty("items").EnumerateArray()).GetProperty("linkedMealId").ValueKind);
        Assert.Equal(JsonValueKind.Null, details.GetProperty("linkedMealId").ValueKind);

        // A new active meal for the same recipe is the linked one again.
        var again = await family.PostAsync("meals/from-recipe", new { RecipeId = lasagne.Id, Icon = "🍝", Color = "#10b981" });
        var relinked = Assert.Single((await family.GetAsync("mealie/recipes?search=lasagne")).GetProperty("items").EnumerateArray());
        Assert.Equal(again.GetProperty("id").GetGuid(), relinked.GetProperty("linkedMealId").GetGuid());
    }

    [Fact]
    public async Task Search_rejects_a_page_below_one()
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture);

        await family.GetAsync("mealie/recipes?page=0", expectedStatus: 400);
    }

    [Fact]
    [CoversEndpoint("GetMealieRecipe")]
    public async Task A_recipe_shows_ingredients_steps_and_its_Mealie_link()
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture);
        var recipe = family.Server.AddRecipe("Lasagne al forno", description: "Layers.");

        var details = await family.GetAsync($"mealie/recipes/{recipe.Id}");

        Assert.Equal("Lasagne al forno", details.GetProperty("name").GetString());
        Assert.Equal("1 hour 30 minutes", details.GetProperty("totalTime").GetString());
        Assert.Equal(["500 g minced beef", "For the sauce"], details.GetProperty("ingredients").EnumerateArray().Select(i => i.GetString()));
        Assert.Single(details.GetProperty("steps").EnumerateArray());
        Assert.Equal($"{family.Server.BaseUrl}/g/holm/r/lasagne-al-forno", details.GetProperty("mealieUrl").GetString());
        Assert.Equal("https://recipes.example/lasagne-al-forno", details.GetProperty("originalUrl").GetString());
    }

    [Fact]
    public async Task A_recipe_is_cached_between_calls()
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture);
        var recipe = family.Server.AddRecipe("Lasagne al forno");

        await family.GetAsync($"mealie/recipes/{recipe.Id}");
        await family.GetAsync($"mealie/recipes/{recipe.Id}");

        Assert.Equal(1, family.Server.RecipeFetches);
    }

    [Fact]
    public async Task A_recipe_deleted_in_Mealie_is_not_found()
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture);

        await family.GetAsync($"mealie/recipes/{Guid.NewGuid()}", expectedStatus: 404);
    }

    [Fact]
    [CoversEndpoint("GetMealieRecipeImage")]
    public async Task The_recipe_photo_is_proxied_with_a_private_cache_header()
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture);
        var recipe = family.Server.AddRecipe("Lasagne al forno");
        var noImage = family.Server.AddRecipe("Toast", image: null);

        var response = await family.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {family.Token}");
            _.Get.Url(family.Path($"mealie/recipes/{recipe.Id}/image"));
            _.StatusCodeShouldBeOk();
            _.ContentTypeShouldBe("image/webp");
        });

        Assert.Equal("private, max-age=3600", response.Context.Response.Headers.CacheControl.ToString());
        await family.GetAsync($"mealie/recipes/{noImage.Id}/image", expectedStatus: 404);
    }

    [Fact]
    [CoversEndpoint("LinkMealToRecipe")]
    public async Task Linking_stores_the_recipe_on_the_meal_and_relinking_the_same_recipe_changes_nothing()
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture);
        var recipe = family.Server.AddRecipe("Lasagne al forno");
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, family.Token, family.ChildId, new CreateMealOptions("Lasagne"));

        var linked = await family.PutAsync($"meals/{meal!.Id}/recipe", new { RecipeId = recipe.Id });
        await family.PutAsync($"meals/{meal.Id}/recipe", new { RecipeId = recipe.Id });

        var link = linked.GetProperty("recipe");
        Assert.Equal(recipe.Id, link.GetProperty("recipeId").GetGuid());
        Assert.Equal("lasagne-al-forno", link.GetProperty("slug").GetString());
        Assert.Equal("Lasagne al forno", link.GetProperty("name").GetString());

        var meals = await family.GetAsync("meals");
        Assert.Equal(recipe.Id, meals[0].GetProperty("recipe").GetProperty("recipeId").GetGuid());
        await AssertMealEventCountAsync(family, meal.Id, expected: 2);
    }

    [Fact]
    public async Task Linking_to_a_recipe_Mealie_does_not_have_is_not_found()
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture);
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, family.Token, family.ChildId);

        await family.PutAsync($"meals/{meal!.Id}/recipe", new { RecipeId = Guid.NewGuid() }, expectedStatus: 404);
    }

    [Fact]
    public async Task Linking_an_archived_meal_or_another_familys_meal_is_not_found()
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture);
        var recipe = family.Server.AddRecipe("Lasagne al forno");
        var archived = await MealplanTestHelpers.CreateMealAsync(fixture, family.Token, family.ChildId);
        await family.DeleteAsync($"meals/{archived!.Id}");
        var other = await MealieTestHost.CreateFamilyAsync(fixture);
        var otherMeal = await MealplanTestHelpers.CreateMealAsync(fixture, other.Token, other.ChildId);

        await family.PutAsync($"meals/{archived.Id}/recipe", new { RecipeId = recipe.Id }, expectedStatus: 404);
        await family.PutAsync($"meals/{otherMeal!.Id}/recipe", new { RecipeId = recipe.Id }, expectedStatus: 404);
    }

    [Fact]
    public async Task Linking_with_an_empty_recipe_id_is_a_validation_error()
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture);
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, family.Token, family.ChildId);

        await family.PutAsync($"meals/{meal!.Id}/recipe", new { RecipeId = Guid.Empty }, expectedStatus: 400);
    }

    [Fact]
    [CoversEndpoint("UnlinkMealRecipe")]
    public async Task Unlinking_works_without_a_connection_and_is_idempotent()
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture);
        var recipe = family.Server.AddRecipe("Lasagne al forno");
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, family.Token, family.ChildId);
        await family.PutAsync($"meals/{meal!.Id}/recipe", new { RecipeId = recipe.Id });
        await family.DeleteAsync("mealie");

        await family.DeleteAsync($"meals/{meal.Id}/recipe");
        await family.DeleteAsync($"meals/{meal.Id}/recipe");

        var meals = await family.GetAsync("meals");
        Assert.Equal(System.Text.Json.JsonValueKind.Null, meals[0].GetProperty("recipe").ValueKind);
        await AssertMealEventCountAsync(family, meal.Id, expected: 3);
    }

    [Fact]
    public async Task A_disconnect_keeps_links_and_a_reconnect_shows_them_again()
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture);
        var recipe = family.Server.AddRecipe("Lasagne al forno");
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, family.Token, family.ChildId);
        await family.PutAsync($"meals/{meal!.Id}/recipe", new { RecipeId = recipe.Id });

        await family.DeleteAsync("mealie");
        await family.GetAsync($"mealie/recipes/{recipe.Id}", expectedStatus: 409);
        Assert.Equal(recipe.Id, (await family.GetAsync("meals"))[0].GetProperty("recipe").GetProperty("recipeId").GetGuid());

        await family.ConnectAsync();
        await family.GetAsync($"mealie/recipes/{recipe.Id}");
    }

    [Fact]
    [CoversEndpoint("CreateMealFromRecipe")]
    public async Task Adding_from_Mealie_creates_a_linked_meal_named_after_the_recipe()
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture);
        var recipe = family.Server.AddRecipe("Lasagne al forno", description: "Layers of pasta.");

        var meal = await family.PostAsync("meals/from-recipe", new { RecipeId = recipe.Id, Icon = "🍝", Color = "#ef4444" });

        Assert.Equal("Lasagne al forno", meal.GetProperty("name").GetString());
        Assert.Equal("Layers of pasta.", meal.GetProperty("description").GetString());
        Assert.Equal("🍝", meal.GetProperty("icon").GetString());
        Assert.Equal(recipe.Id, meal.GetProperty("recipe").GetProperty("recipeId").GetGuid());
        await AssertMealEventCountAsync(family, meal.GetProperty("id").GetGuid(), expected: 2);
    }

    [Fact]
    public async Task Adding_from_Mealie_checks_the_icon_like_any_new_meal()
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture);
        var recipe = family.Server.AddRecipe("Lasagne al forno");

        var problem = await family.PostAsync("meals/from-recipe", new { RecipeId = recipe.Id, Icon = new string('x', 80), Color = "#ef4444" }, expectedStatus: 400);
        await family.PostAsync("meals/from-recipe", new { RecipeId = Guid.Empty, Icon = "🍝", Color = "#ef4444" }, expectedStatus: 400);
        await family.PostAsync("meals/from-recipe", new { RecipeId = Guid.NewGuid(), Icon = "🍝", Color = "#ef4444" }, expectedStatus: 404);

        Assert.True(problem.GetProperty("details").TryGetProperty("icon", out _));
    }

    [Fact]
    public async Task Every_Mealie_route_answers_409_while_the_family_is_not_connected()
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture, connect: false);
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, family.Token, family.ChildId);
        var recipeId = Guid.NewGuid();

        var problem = await family.GetAsync("mealie/recipes", expectedStatus: 409);
        await family.GetAsync($"mealie/recipes/{recipeId}", expectedStatus: 409);
        await family.GetAsync($"mealie/recipes/{recipeId}/image", expectedStatus: 409);
        await family.PutAsync($"meals/{meal!.Id}/recipe", new { RecipeId = recipeId }, expectedStatus: 409);
        await family.PostAsync("meals/from-recipe", new { RecipeId = recipeId, Icon = "🍝", Color = "#ef4444" }, expectedStatus: 409);
        await family.GetAsync("mealie/shopping-lists", expectedStatus: 409);
        await family.PostAsync("imports/mealie/preview", new { From = "2026-10-19", To = "2026-10-25" }, expectedStatus: 409);
        await family.PostAsync($"mealie/shopping-lists/{Guid.NewGuid()}/recipes", new { From = "2026-10-19", To = "2026-10-25", Slots = new[] { "Dinner" } }, expectedStatus: 409);

        Assert.Equal(MealieErrorCodes.NotConnected, problem.GetProperty("code").GetString());
    }

    [Theory]
    [InlineData(MealieFailureKind.Unreachable, "mealie_unreachable")]
    [InlineData(MealieFailureKind.TokenRejected, "mealie_token_rejected")]
    [InlineData(MealieFailureKind.Error, "mealie_error")]
    public async Task A_failing_server_is_a_502_with_a_code_saying_why(MealieFailureKind kind, string code)
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture);
        var recipe = family.Server.AddRecipe("Lasagne al forno");

        switch (kind)
        {
            case MealieFailureKind.Unreachable:
                family.Server.Down = true;
                break;
            case MealieFailureKind.TokenRejected:
                family.Server.ValidTokens.Clear();
                break;
            default:
                family.Server.FailWith = kind;
                break;
        }

        var problem = await family.GetAsync($"mealie/recipes/{recipe.Id}", expectedStatus: 502);
        var image = await family.GetAsync($"mealie/recipes/{recipe.Id}/image", expectedStatus: 502);
        await family.GetAsync("mealie/recipes", expectedStatus: 502);

        Assert.Equal(code, problem.GetProperty("code").GetString());
        Assert.Equal(code, image.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("message").GetString()));
    }

    [Fact]
    public async Task A_child_is_forbidden_and_an_unrelated_guardian_gets_not_found()
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture);
        var recipe = family.Server.AddRecipe("Lasagne al forno");
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, family.Token, family.ChildId);
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, family.Child);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();

        foreach (var (token, status) in new[] { (childToken, 403), (strangerToken, 404) })
        {
            await family.GetAsync("mealie/recipes", expectedStatus: status, token: token);
            await family.GetAsync($"mealie/recipes/{recipe.Id}", expectedStatus: status, token: token);
            await family.GetAsync($"mealie/recipes/{recipe.Id}/image", expectedStatus: status, token: token);
            await family.PutAsync($"meals/{meal!.Id}/recipe", new { RecipeId = recipe.Id }, expectedStatus: status, token: token);
            await family.DeleteAsync($"meals/{meal.Id}/recipe", expectedStatus: status, token: token);
            await family.PostAsync("meals/from-recipe", new { RecipeId = recipe.Id, Icon = "🍝", Color = "#ef4444" }, expectedStatus: status, token: token);
        }
    }

    private static async Task AssertMealEventCountAsync(MealieFamily family, Guid mealId, int expected)
    {
        var store = (IMealEventStore)family.Host.Services.GetService(typeof(IMealEventStore))!;
        var events = await store.ReadAsync(new MealId(mealId), CancellationToken.None);

        Assert.Equal(expected, events.Count);
    }
}
