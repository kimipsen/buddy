using Alba;

using buddy.Common;
using buddy.IntegrationTests.Features.Guardians;
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

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task A_missing_or_blank_comment_is_stored_as_empty_and_re_rating_with_either_is_a_no_op(string? blank)
    {
        // Free text is normalized at the endpoint (FreeText): null, missing and whitespace-only all
        // become "", so RateMeal's before == after idempotency check can't see them as different.
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(meal);
        var url = $"/mealplans/children/{child.Id}/meals/{meal.Id}/rating";

        var first = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Put.Json(new { Stars = 4, Comment = blank }).ToUrl(url);
            _.StatusCodeShouldBeOk();
        });
        var firstRating = Assert.Single(first.ReadAsJson<MealDto>().Ratings);
        Assert.Equal("", firstRating.Comment);

        var second = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Put.Json(new { Stars = 4, Comment = blank is null ? "  " : null }).ToUrl(url);
            _.StatusCodeShouldBeOk();
        });

        Assert.Equal(firstRating.RatedAt, Assert.Single(second.ReadAsJson<MealDto>().Ratings).RatedAt);
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

    [Fact]
    public async Task A_rating_of_zero_stars_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(meal);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Put.Json(new { Stars = 0, Comment = (string?)null }).ToUrl($"/mealplans/children/{child.Id}/meals/{meal.Id}/rating");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Equal(["Stars must be between 1 and 5."], error.Details["Stars"]);
    }

    [Fact]
    public async Task A_one_star_rating_with_a_2000_character_comment_is_accepted()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(meal);
        var comment = new string('c', 2000);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Put.Json(new { Stars = 1, Comment = comment }).ToUrl($"/mealplans/children/{child.Id}/meals/{meal.Id}/rating");
            _.StatusCodeShouldBeOk();
        });

        var rating = Assert.Single(response.ReadAsJson<MealDto>().Ratings);
        Assert.Equal(1, rating.Stars);
        Assert.Equal(comment, rating.Comment);
    }

    [Fact]
    public async Task A_comment_longer_than_2000_characters_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(meal);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Put.Json(new { Stars = 4, Comment = new string('c', 2001) }).ToUrl($"/mealplans/children/{child.Id}/meals/{meal.Id}/rating");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Contains("Comment", error.Details.Keys);
    }
}
