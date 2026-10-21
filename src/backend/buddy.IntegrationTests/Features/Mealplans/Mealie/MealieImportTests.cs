using System.Text.Json;

using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Mealplans.Mealie;

// docs/backend/analysis/mealie-integration.md, Question 5: a Mealie plan goes through the same
// review and commit as a pasted note.
[Collection(BuddyApiCollection.Name)]
public sealed class MealieImportTests(BuddyApiFixture fixture)
{
    private static readonly DateOnly Monday = new(2031, 3, 3);

    [Fact]
    [CoversEndpoint("PreviewMealieImport")]
    public async Task The_preview_maps_entry_types_to_slots_and_merges_side_dishes_into_dinner()
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture);
        var lasagne = family.Server.AddRecipe("Lasagne al forno");
        family.Server.AddPlanEntry(Monday, "breakfast", title: "Oatmeal");
        family.Server.AddPlanEntry(Monday, "dinner", lasagne, text: "Double batch");
        family.Server.AddPlanEntry(Monday, "side", title: "Green salad");
        family.Server.AddPlanEntry(Monday.AddDays(1), "dessert", title: "Ice cream");
        family.Server.AddPlanEntry(Monday.AddDays(1), "lunch", title: "Rye bread");
        family.Server.AddPlanEntry(Monday.AddDays(1), "lunch", title: "Leftovers");
        family.Server.AddPlanEntry(Monday.AddDays(2), "dinner");

        var result = await family.PostAsync("imports/mealie/preview", new { From = Monday, To = Monday.AddDays(6) });
        var preview = result.GetProperty("preview");
        var lines = preview.GetProperty("lines").EnumerateArray().ToList();

        Assert.Equal("mealie", preview.GetProperty("format").GetString());
        Assert.Equal(["Oatmeal", "Lasagne al forno", "Rye bread"], lines.Select(l => l.GetProperty("mealName").GetString()));
        Assert.Equal(["Breakfast", "Dinner", "Lunch"], lines.Select(l => l.GetProperty("slot").GetString()));
        Assert.Equal("Double batch + side: Green salad", lines[1].GetProperty("notes").GetString());
        Assert.Equal("+ Leftovers", lines[2].GetProperty("notes").GetString());

        var warnings = preview.GetProperty("warnings").EnumerateArray().Select(w => w.GetProperty("code").GetString()).ToList();
        Assert.Contains("mealie_entry_merged", warnings);
        Assert.Contains("duplicate_slot", warnings);
        Assert.Equal(2, warnings.Count(w => w == "mealie_entry_skipped"));

        var recipe = Assert.Single(result.GetProperty("recipes").EnumerateArray());
        Assert.Equal(lasagne.Id, recipe.GetProperty("recipe").GetProperty("recipeId").GetGuid());
        Assert.Equal(lines[1].GetProperty("key").GetString(), recipe.GetProperty("key").GetString());
    }

    [Fact]
    public async Task An_entry_whose_recipe_is_linked_matches_that_meal_even_under_another_name()
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture);
        var lasagne = family.Server.AddRecipe("Lasagne al forno");
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, family.Token, family.ChildId, new CreateMealOptions("Mormors lasagne"));
        await family.PutAsync($"meals/{meal!.Id}/recipe", new { RecipeId = lasagne.Id });
        family.Server.AddPlanEntry(Monday, "dinner", lasagne);

        var result = await family.PostAsync("imports/mealie/preview", new { From = Monday, To = Monday });
        var group = Assert.Single(result.GetProperty("preview").GetProperty("groups").EnumerateArray());

        Assert.Equal("Existing", group.GetProperty("defaultAction").GetString());
        Assert.Equal(meal.Id, group.GetProperty("matchedMealId").GetGuid());
    }

    [Fact]
    public async Task An_empty_range_is_a_preview_with_a_warning()
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture);

        var result = await family.PostAsync("imports/mealie/preview", new { From = Monday, To = Monday.AddDays(6) });

        Assert.Empty(result.GetProperty("preview").GetProperty("lines").EnumerateArray());
        Assert.Equal("mealie_range_empty", result.GetProperty("preview").GetProperty("warnings")[0].GetProperty("code").GetString());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(92)]
    public async Task A_range_backwards_or_longer_than_a_quarter_is_rejected(int days)
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture);

        await family.PostAsync("imports/mealie/preview", new { From = Monday, To = Monday.AddDays(days) }, expectedStatus: 400);
    }

    [Fact]
    public async Task A_Mealie_404_on_the_plan_is_reported_as_an_error_not_as_not_found()
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture);
        family.Server.FailWith = buddy.Features.Mealplans.MealieFailureKind.NotFound;

        var problem = await family.PostAsync("imports/mealie/preview", new { From = Monday, To = Monday }, expectedStatus: 502);

        Assert.Equal("mealie_error", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Committing_a_Mealie_import_creates_new_meals_already_linked_and_reverting_archives_them()
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture);
        var lasagne = family.Server.AddRecipe("Lasagne al forno");

        var result = await family.PostAsync("imports", new
        {
            Format = "mealie",
            ArchiveSingleUse = false,
            Entries = new object[]
            {
                new { Date = Monday, Slot = "Dinner", NewMealName = "Lasagne al forno", MealieRecipe = new { RecipeId = lasagne.Id, Slug = lasagne.Slug, Name = lasagne.Name } },
                new { Date = Monday.AddDays(2), Slot = "Dinner", NewMealName = "Lasagne al forno" },
                new { Date = Monday.AddDays(1), Slot = "Dinner", NewMealName = "Fish fingers" }
            }
        }, idempotencyKey: Guid.NewGuid().ToString());

        Assert.Equal(3, result.GetProperty("imported").GetInt32());
        Assert.Equal(2, result.GetProperty("createdMeals").GetInt32());

        var meals = (await family.GetAsync("meals")).EnumerateArray().ToList();
        var linked = meals.Single(m => m.GetProperty("name").GetString() == "Lasagne al forno");
        var unlinked = meals.Single(m => m.GetProperty("name").GetString() == "Fish fingers");
        Assert.Equal(lasagne.Id, linked.GetProperty("recipe").GetProperty("recipeId").GetGuid());
        Assert.Equal(JsonValueKind.Null, unlinked.GetProperty("recipe").ValueKind);

        var imports = await family.GetAsync("imports");
        Assert.Equal("mealie", imports[0].GetProperty("format").GetString());

        await family.DeleteAsync($"imports/{result.GetProperty("importId").GetGuid()}");
        Assert.All((await family.GetAsync("meals")).EnumerateArray(), m => Assert.True(m.GetProperty("isArchived").GetBoolean()));
    }

    [Fact]
    public async Task Only_a_Mealie_import_can_link_new_meals_to_recipes()
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture);

        var problem = await family.PostAsync("imports", new
        {
            Format = "csv",
            Entries = new object[]
            {
                new { Date = Monday, Slot = "Dinner", NewMealName = "Lasagne", MealieRecipe = new { RecipeId = Guid.NewGuid(), Slug = "x", Name = "X" } }
            }
        }, expectedStatus: 400, idempotencyKey: Guid.NewGuid().ToString());

        Assert.Contains("Only a Mealie import", problem.GetProperty("details").GetProperty("entries")[0].GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_recipe_on_an_existing_meal_entry_is_rejected()
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture);
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, family.Token, family.ChildId);

        var problem = await family.PostAsync("imports", new
        {
            Format = "mealie",
            Entries = new object[]
            {
                new { Date = Monday, Slot = "Dinner", MealId = meal!.Id, MealieRecipe = new { RecipeId = Guid.NewGuid(), Slug = "x", Name = "X" } }
            }
        }, expectedStatus: 400, idempotencyKey: Guid.NewGuid().ToString());

        Assert.Contains("mealieRecipe", problem.GetProperty("details").EnumerateObject().Single().Name, StringComparison.Ordinal);
    }
}
