using Alba;

using buddy.Features.Mealplans;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Mealplans.Mealie;

// docs/backend/analysis/mealie-integration.md, Question 6.
[Collection(BuddyApiCollection.Name)]
public sealed class MealieShoppingTests(BuddyApiFixture fixture)
{
    private static readonly DateOnly Monday = new(2031, 3, 3);

    [Fact]
    [CoversEndpoint("ListMealieShoppingLists")]
    public async Task The_households_shopping_lists_are_listed()
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture);

        var lists = await family.GetAsync("mealie/shopping-lists");

        var list = Assert.Single(lists.EnumerateArray());
        Assert.Equal("Weekly shop", list.GetProperty("name").GetString());
        Assert.Equal(family.Server.ShoppingLists[0].Id, list.GetProperty("id").GetGuid());
    }

    [Fact]
    [CoversEndpoint("AddMealPlanToMealieShoppingList")]
    public async Task Linked_recipes_go_to_the_list_once_each_with_how_often_they_are_planned()
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture);
        var lasagneRecipe = family.Server.AddRecipe("Lasagne al forno");
        var lasagne = await MealWithRecipeAsync(family, "Lasagne", lasagneRecipe);
        var fishFingers = await MealplanTestHelpers.CreateMealAsync(fixture, family.Token, family.ChildId, new CreateMealOptions("Fish fingers"));
        var grandma = await MealplanTestHelpers.CreateMealAsync(fixture, family.Token, family.ChildId, new CreateMealOptions("Dinner at Grandma's", EatingOut: MealEatingOut.FriendsAndFamily));
        await AssignAsync(family, Monday, MealSlot.Dinner, lasagne);
        await AssignAsync(family, Monday.AddDays(3), MealSlot.Dinner, lasagne);
        await AssignAsync(family, Monday.AddDays(1), MealSlot.Dinner, fishFingers!.Id);
        await AssignAsync(family, Monday.AddDays(2), MealSlot.Dinner, grandma!.Id);
        await AssignAsync(family, Monday.AddDays(2), MealSlot.Lunch, lasagne);
        await AssignAsync(family, Monday.AddDays(8), MealSlot.Dinner, lasagne);
        var listId = family.Server.ShoppingLists[0].Id;

        var result = await family.PostAsync(
            $"mealie/shopping-lists/{listId}/recipes",
            new { From = Monday, To = Monday.AddDays(6), Slots = new[] { "Dinner" } },
            idempotencyKey: Guid.NewGuid().ToString());

        var added = Assert.Single(result.GetProperty("added").EnumerateArray());
        Assert.Equal(lasagneRecipe.Id, added.GetProperty("recipeId").GetGuid());
        Assert.Equal(2, added.GetProperty("times").GetInt32());
        Assert.Equal(["Fish fingers"], result.GetProperty("skippedUnlinked").EnumerateArray().Select(m => m.GetString()));
        Assert.Equal(1, result.GetProperty("skippedEatingOut").GetInt32());

        var call = Assert.Single(family.Server.ShoppingCalls);
        Assert.Equal(listId, call.ListId);
        Assert.Equal([new MealieShoppingListRecipeDto(lasagneRecipe.Id, 2)], call.Recipes);
    }

    [Fact]
    public async Task A_repeated_idempotency_key_does_not_add_the_recipes_twice()
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture);
        var meal = await MealWithRecipeAsync(family, "Lasagne", family.Server.AddRecipe("Lasagne al forno"));
        await AssignAsync(family, Monday, MealSlot.Dinner, meal);
        var path = $"mealie/shopping-lists/{family.Server.ShoppingLists[0].Id}/recipes";
        var body = new { From = Monday, To = Monday, Slots = new[] { "Dinner" } };
        var key = Guid.NewGuid().ToString();

        await family.PostAsync(path, body, idempotencyKey: key);
        await family.PostAsync(path, body, idempotencyKey: key);

        Assert.Single(family.Server.ShoppingCalls);
    }

    [Fact]
    public async Task Nothing_linked_in_the_range_makes_no_Mealie_call()
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture);

        var result = await family.PostAsync(
            $"mealie/shopping-lists/{family.Server.ShoppingLists[0].Id}/recipes",
            new { From = Monday, To = Monday.AddDays(6), Slots = new[] { "Dinner" } },
            idempotencyKey: Guid.NewGuid().ToString());

        Assert.Empty(result.GetProperty("added").EnumerateArray());
        Assert.Empty(family.Server.ShoppingCalls);
    }

    [Fact]
    public async Task A_list_deleted_in_Mealie_is_a_502_asking_for_another_list()
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture);
        var meal = await MealWithRecipeAsync(family, "Lasagne", family.Server.AddRecipe("Lasagne al forno"));
        await AssignAsync(family, Monday, MealSlot.Dinner, meal);

        var problem = await family.PostAsync(
            $"mealie/shopping-lists/{Guid.NewGuid()}/recipes",
            new { From = Monday, To = Monday, Slots = new[] { "Dinner" } },
            expectedStatus: 502,
            idempotencyKey: Guid.NewGuid().ToString());

        Assert.Equal("mealie_error", problem.GetProperty("code").GetString());
        Assert.Contains("no longer exists", problem.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_server_error_on_a_list_that_still_exists_is_reported_as_it_is()
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture);
        var meal = await MealWithRecipeAsync(family, "Lasagne", family.Server.AddRecipe("Lasagne al forno"));
        await AssignAsync(family, Monday, MealSlot.Dinner, meal);
        family.Server.FailWith = MealieFailureKind.Error;

        var problem = await family.PostAsync(
            $"mealie/shopping-lists/{family.Server.ShoppingLists[0].Id}/recipes",
            new { From = Monday, To = Monday, Slots = new[] { "Dinner" } },
            expectedStatus: 502,
            idempotencyKey: Guid.NewGuid().ToString());

        Assert.Equal("mealie_error", problem.GetProperty("code").GetString());
        Assert.DoesNotContain("no longer exists", problem.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1, "Dinner")]
    [InlineData(31, "Dinner")]
    [InlineData(0, null)]
    public async Task An_invalid_range_or_no_slots_is_a_validation_error(int days, string? slot)
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture);

        await family.PostAsync(
            $"mealie/shopping-lists/{family.Server.ShoppingLists[0].Id}/recipes",
            new { From = Monday, To = Monday.AddDays(days), Slots = slot is null ? Array.Empty<string>() : [slot] },
            expectedStatus: 400,
            idempotencyKey: Guid.NewGuid().ToString());
    }

    private async Task<Guid> MealWithRecipeAsync(MealieFamily family, string name, MealieRecipeDto recipe)
    {
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, family.Token, family.ChildId, new CreateMealOptions(name));
        await family.PutAsync($"meals/{meal!.Id}/recipe", new { RecipeId = recipe.Id });
        return meal.Id;
    }

    private static Task<IScenarioResult> AssignAsync(MealieFamily family, DateOnly date, MealSlot slot, Guid mealId) =>
        family.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {family.Token}");
            _.Put.Json(new { MealId = mealId, Notes = "" })
                .ToUrl(family.Path("plan"))
                .QueryString("date", date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture))
                .QueryString("slot", slot.ToString());
            _.StatusCodeShouldBeOk();
        });
}
