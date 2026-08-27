using Alba;

using buddy.Common;
using buddy.Features.Mealplans;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.Mealplans;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Mealplans.AssignMealToSlot;

[Collection(BuddyApiCollection.Name)]
public sealed class AssignMealToSlotTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("AssignMealToSlot")]
    public async Task A_guardian_can_assign_a_meal_to_a_dinner_slot()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(meal);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { MealId = meal.Id, Notes = "No cilantro" })
                .ToUrl($"/mealplans/children/{child.Id}/plan")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "Dinner");
            _.StatusCodeShouldBeOk();
        });

        var entry = response.ReadAsJson<MealPlanEntryDto>();
        Assert.Equal(today, entry.Date);
        Assert.Equal(MealSlot.Dinner, entry.Slot);
        Assert.Equal(meal.Id, entry.MealId);
        Assert.Equal("Tacos", entry.MealName);
        Assert.Equal("No cilantro", entry.Notes);
    }

    [Fact]
    public async Task Reassigning_the_same_slot_overwrites_the_previous_meal()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var tacos = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id, new CreateMealOptions(Name: "Tacos"));
        var pasta = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id, new CreateMealOptions(Name: "Pasta"));
        Assert.NotNull(tacos);
        Assert.NotNull(pasta);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { MealId = tacos.Id, Notes = (string?)null })
                .ToUrl($"/mealplans/children/{child.Id}/plan")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "Dinner");
            _.StatusCodeShouldBeOk();
        });

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { MealId = pasta.Id, Notes = (string?)null })
                .ToUrl($"/mealplans/children/{child.Id}/plan")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "Dinner");
            _.StatusCodeShouldBeOk();
        });

        Assert.Equal(pasta.Id, response.ReadAsJson<MealPlanEntryDto>().MealId);

        var listResponse = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/mealplans/children/{child.Id}/plan?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}");
            _.StatusCodeShouldBeOk();
        });

        var entry = Assert.Single(listResponse.ReadAsJson<List<MealPlanEntryDto>>());
        Assert.Equal(pasta.Id, entry.MealId);
    }

    [Fact]
    public async Task Assigning_a_second_slot_on_an_already_existing_plan_adds_to_it()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var pancakes = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id, new CreateMealOptions(Name: "Pancakes"));
        var tacos = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id, new CreateMealOptions(Name: "Tacos"));
        Assert.NotNull(pancakes);
        Assert.NotNull(tacos);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // First assignment creates the plan; this is the "before is null" case for a slot that
        // hasn't been touched yet, on a plan that already exists.
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { MealId = tacos.Id, Notes = (string?)null })
                .ToUrl($"/mealplans/children/{child.Id}/plan")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "Dinner");
            _.StatusCodeShouldBeOk();
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { MealId = pancakes.Id, Notes = (string?)null })
                .ToUrl($"/mealplans/children/{child.Id}/plan")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "Breakfast");
            _.StatusCodeShouldBeOk();
        });

        var listResponse = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/mealplans/children/{child.Id}/plan?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}");
            _.StatusCodeShouldBeOk();
        });

        var entries = listResponse.ReadAsJson<List<MealPlanEntryDto>>();
        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e.Slot == MealSlot.Dinner && e.MealId == tacos.Id);
        Assert.Contains(entries, e => e.Slot == MealSlot.Breakfast && e.MealId == pancakes.Id);
    }

    [Fact]
    public async Task Reassigning_the_same_meal_with_different_notes_updates_the_notes()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(meal);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { MealId = meal.Id, Notes = "No cilantro" })
                .ToUrl($"/mealplans/children/{child.Id}/plan")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "Dinner");
            _.StatusCodeShouldBeOk();
        });

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { MealId = meal.Id, Notes = "Extra spicy" })
                .ToUrl($"/mealplans/children/{child.Id}/plan")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "Dinner");
            _.StatusCodeShouldBeOk();
        });

        Assert.Equal("Extra spicy", response.ReadAsJson<MealPlanEntryDto>().Notes);
    }

    [Fact]
    public async Task Reassigning_the_exact_same_meal_and_notes_is_a_no_op()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(meal);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        for (var i = 0; i < 2; i++)
        {
            await fixture.Host.Scenario(_ =>
            {
                _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
                _.Put.Json(new { MealId = meal.Id, Notes = "No cilantro" })
                    .ToUrl($"/mealplans/children/{child.Id}/plan")
                    .QueryString("date", $"{today:yyyy-MM-dd}")
                    .QueryString("slot", "Dinner");
                _.StatusCodeShouldBeOk();
            });
        }

        var listResponse = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/mealplans/children/{child.Id}/plan?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}");
            _.StatusCodeShouldBeOk();
        });

        var entry = Assert.Single(listResponse.ReadAsJson<List<MealPlanEntryDto>>());
        Assert.Equal("No cilantro", entry.Notes);
    }

    [Fact]
    public async Task Assigning_an_archived_meal_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(meal);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Delete.Url($"/mealplans/children/{child.Id}/meals/{meal.Id}");
            _.StatusCodeShouldBe(204);
        });

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { MealId = meal.Id, Notes = (string?)null })
                .ToUrl($"/mealplans/children/{child.Id}/plan")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "Dinner");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Equal(["Cannot assign an archived meal."], error.Details[""]);
    }

    [Fact]
    public async Task The_child_cannot_assign_a_meal_to_the_plan()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(meal);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Put.Json(new { MealId = meal.Id, Notes = (string?)null })
                .ToUrl($"/mealplans/children/{child.Id}/plan")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "Dinner");
            _.StatusCodeShouldBe(403);
        });
    }
}
