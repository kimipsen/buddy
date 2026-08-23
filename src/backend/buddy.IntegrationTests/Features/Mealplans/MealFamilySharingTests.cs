using Alba;

using buddy.Features.Mealplans;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;

using Xunit;

namespace buddy.IntegrationTests.Features.Mealplans;

// Verifies meals and the meal plan are shared across siblings -- a guardian shouldn't need to
// recreate a meal or a plan entry per child. See docs/backend/analysis/mealplans.md.
[Collection(BuddyApiCollection.Name)]
public sealed class MealFamilySharingTests(BuddyApiFixture fixture)
{
    [Fact]
    public async Task A_meal_created_for_one_child_is_visible_in_their_siblings_library()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var alice = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alice");
        var bob = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Bob");

        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, alice.Id);
        Assert.NotNull(meal);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/mealplans/children/{bob.Id}/meals");
            _.StatusCodeShouldBeOk();
        });

        var listed = Assert.Single(response.ReadAsJson<List<MealDto>>());
        Assert.Equal(meal.Id, listed.Id);
    }

    [Fact]
    public async Task Assigning_a_meal_via_one_childs_plan_shows_up_on_their_siblings_plan_too()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var alice = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alice");
        var bob = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Bob");
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, alice.Id);
        Assert.NotNull(meal);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { MealId = meal.Id, Notes = (string?)null })
                .ToUrl($"/mealplans/children/{alice.Id}/plan")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "Dinner");
            _.StatusCodeShouldBeOk();
        });

        // No plan was ever assigned "for Bob" directly -- it's the same family plan Alice's
        // guardian just wrote to.
        var bobsPlan = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/mealplans/children/{bob.Id}/plan?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}");
            _.StatusCodeShouldBeOk();
        });

        var entry = Assert.Single(bobsPlan.ReadAsJson<List<MealPlanEntryDto>>());
        Assert.Equal(meal.Id, entry.MealId);

        // Clearing it from Bob's URL clears the one shared plan -- Alice sees it gone too.
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Delete.Url($"/mealplans/children/{bob.Id}/plan")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "Dinner");
            _.StatusCodeShouldBe(204);
        });

        var alicesPlan = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/mealplans/children/{alice.Id}/plan?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}");
            _.StatusCodeShouldBeOk();
        });

        Assert.Empty(alicesPlan.ReadAsJson<List<MealPlanEntryDto>>());
    }

    [Fact]
    public async Task Each_sibling_rates_a_shared_meal_independently()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var alice = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alice");
        var bob = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Bob");
        var aliceToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, alice);
        var bobToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, bob);
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, alice.Id);
        Assert.NotNull(meal);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {aliceToken}");
            _.Put.Json(new { Stars = 5, Comment = "Loved it!" }).ToUrl($"/mealplans/children/{alice.Id}/meals/{meal.Id}/rating");
            _.StatusCodeShouldBeOk();
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {bobToken}");
            _.Put.Json(new { Stars = 2, Comment = "Not for me" }).ToUrl($"/mealplans/children/{bob.Id}/meals/{meal.Id}/rating");
            _.StatusCodeShouldBeOk();
        });

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/mealplans/children/{alice.Id}/meals");
            _.StatusCodeShouldBeOk();
        });

        var listed = Assert.Single(response.ReadAsJson<List<MealDto>>());
        Assert.Equal(2, listed.Ratings.Count);
        Assert.Contains(listed.Ratings, r => r.ChildId == alice.Id && r.Stars == 5);
        Assert.Contains(listed.Ratings, r => r.ChildId == bob.Id && r.Stars == 2);
    }

    [Fact]
    public async Task A_child_with_a_different_guardian_does_not_see_someone_elses_family_meal()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var alice = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alice");
        await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, alice.Id);

        var (_, otherGuardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var carol = await GuardianTestHelpers.CreateChildAsync(fixture, otherGuardianToken, "Carol");

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {otherGuardianToken}");
            _.Get.Url($"/mealplans/children/{carol.Id}/meals");
            _.StatusCodeShouldBeOk();
        });

        Assert.Empty(response.ReadAsJson<List<MealDto>>());
    }
}
