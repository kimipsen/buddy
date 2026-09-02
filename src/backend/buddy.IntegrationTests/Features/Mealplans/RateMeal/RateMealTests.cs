using Alba;

using buddy.Common;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.Mealplans;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Mealplans.RateMeal;

[Collection(BuddyApiCollection.Name)]
public sealed class RateMealTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("RateMeal")]
    public async Task The_child_can_rate_a_meal()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(meal);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Put.Json(new { Stars = 5, Comment = "Loved it!" }).ToUrl($"/mealplans/children/{child.Id}/meals/{meal.Id}/rating");
            _.StatusCodeShouldBeOk();
        });

        var rated = response.ReadAsJson<MealDto>();
        var rating = Assert.Single(rated.Ratings);
        Assert.Equal(child.Id, rating.ChildId);
        Assert.Equal(5, rating.Stars);
        Assert.Equal("Loved it!", rating.Comment);
    }

    [Fact]
    public async Task Rating_the_same_meal_with_the_same_stars_and_comment_again_does_not_change_RatedAt()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(meal);

        var first = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Put.Json(new { Stars = 5, Comment = "Loved it!" }).ToUrl($"/mealplans/children/{child.Id}/meals/{meal.Id}/rating");
            _.StatusCodeShouldBeOk();
        });
        var firstRating = Assert.Single(first.ReadAsJson<MealDto>().Ratings);

        // Retrying the exact same rating (e.g. a client double-tap or network retry) must be a
        // true no-op, not just the same Stars/Comment with a bumped RatedAt.
        var second = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Put.Json(new { Stars = 5, Comment = "Loved it!" }).ToUrl($"/mealplans/children/{child.Id}/meals/{meal.Id}/rating");
            _.StatusCodeShouldBeOk();
        });
        var secondRating = Assert.Single(second.ReadAsJson<MealDto>().Ratings);

        Assert.Equal(firstRating.RatedAt, secondRating.RatedAt);
    }

    [Fact]
    public async Task A_rating_outside_one_to_five_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(meal);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Put.Json(new { Stars = 6, Comment = (string?)null }).ToUrl($"/mealplans/children/{child.Id}/meals/{meal.Id}/rating");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Equal(["Stars must be between 1 and 5."], error.Details["Stars"]);
    }

    [Fact]
    public async Task A_guardian_cannot_rate_a_meal_on_the_childs_behalf()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(meal);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { Stars = 5, Comment = (string?)null }).ToUrl($"/mealplans/children/{child.Id}/meals/{meal.Id}/rating");
            _.StatusCodeShouldBe(403);
        });
    }
}
