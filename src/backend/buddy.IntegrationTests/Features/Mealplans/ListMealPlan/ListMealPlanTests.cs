using Alba;

using buddy.Features.Mealplans;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.Mealplans;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Mealplans.ListMealPlan;

[Collection(BuddyApiCollection.Name)]
public sealed class ListMealPlanTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ListMealPlan")]
    public async Task The_child_can_see_their_own_plan_for_a_date_range()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(meal);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { MealId = meal.Id, Notes = (string?)null })
                .ToUrl($"/mealplans/children/{child.Id}/plan")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "Dinner");
            _.StatusCodeShouldBeOk();
        });

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Get.Url($"/mealplans/children/{child.Id}/plan?from={today:yyyy-MM-dd}&to={today.AddDays(1):yyyy-MM-dd}");
            _.StatusCodeShouldBeOk();
        });

        var entry = Assert.Single(response.ReadAsJson<List<MealPlanEntryDto>>());
        Assert.Equal(MealSlot.Dinner, entry.Slot);
        Assert.Equal(meal.Id, entry.MealId);
    }

    [Fact]
    public async Task A_plan_entry_includes_every_sibling_who_rated_the_meal()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var alex = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var sam = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Sam");
        var alexToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, alex);
        var samToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, sam);
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, alex.Id);
        Assert.NotNull(meal);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { MealId = meal.Id, Notes = (string?)null })
                .ToUrl($"/mealplans/children/{alex.Id}/plan")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "Dinner");
            _.StatusCodeShouldBeOk();
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {alexToken}");
            _.Put.Json(new { Stars = 5, Comment = "Loved it!" }).ToUrl($"/mealplans/children/{alex.Id}/meals/{meal.Id}/rating");
            _.StatusCodeShouldBeOk();
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {samToken}");
            _.Put.Json(new { Stars = 2, Comment = "Not a fan" }).ToUrl($"/mealplans/children/{sam.Id}/meals/{meal.Id}/rating");
            _.StatusCodeShouldBeOk();
        });

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/mealplans/children/{alex.Id}/plan?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}");
            _.StatusCodeShouldBeOk();
        });

        var entry = Assert.Single(response.ReadAsJson<List<MealPlanEntryDto>>());
        Assert.Equal(2, entry.AllRatings.Count);
        Assert.Contains(entry.AllRatings, rating => rating.ChildId == alex.Id && rating.Stars == 5);
        Assert.Contains(entry.AllRatings, rating => rating.ChildId == sam.Id && rating.Stars == 2);
    }

    [Fact]
    public async Task Entries_outside_the_requested_range_are_excluded()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(meal);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { MealId = meal.Id, Notes = (string?)null })
                .ToUrl($"/mealplans/children/{child.Id}/plan")
                .QueryString("date", $"{today.AddDays(5):yyyy-MM-dd}")
                .QueryString("slot", "Dinner");
            _.StatusCodeShouldBeOk();
        });

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/mealplans/children/{child.Id}/plan?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}");
            _.StatusCodeShouldBeOk();
        });

        Assert.Empty(response.ReadAsJson<List<MealPlanEntryDto>>());
    }

    [Fact]
    public async Task A_child_with_no_plan_yet_gets_an_empty_list()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/mealplans/children/{child.Id}/plan?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}");
            _.StatusCodeShouldBeOk();
        });

        Assert.Empty(response.ReadAsJson<List<MealPlanEntryDto>>());
    }

    [Fact]
    public async Task Rejects_a_range_where_to_is_before_from()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/mealplans/children/{child.Id}/plan?from={today:yyyy-MM-dd}&to={today.AddDays(-1):yyyy-MM-dd}");
            _.StatusCodeShouldBe(400);
        });

        Assert.Equal("'to' must not be before 'from'.", response.ReadAsJson<string>());
    }

    [Fact]
    public async Task Rejects_a_range_longer_than_the_maximum()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/mealplans/children/{child.Id}/plan?from={today:yyyy-MM-dd}&to={today.AddDays(ListMealPlanHandler.MaxRangeDays + 1):yyyy-MM-dd}");
            _.StatusCodeShouldBe(400);
        });

        Assert.Equal($"The requested range cannot exceed {ListMealPlanHandler.MaxRangeDays} days.", response.ReadAsJson<string>());
    }

    [Fact]
    public async Task A_third_party_with_no_guardian_link_gets_not_found()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {strangerToken}");
            _.Get.Url($"/mealplans/children/{child.Id}/plan?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}");
            _.StatusCodeShouldBe(404);
        });
    }
}
