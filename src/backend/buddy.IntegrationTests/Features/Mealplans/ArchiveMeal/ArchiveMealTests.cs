using Alba;

using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.Mealplans;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Mealplans.ArchiveMeal;

[Collection(BuddyApiCollection.Name)]
public sealed class ArchiveMealTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ArchiveMeal")]
    public async Task Archiving_a_meal_keeps_it_in_the_library_but_blocks_new_assignments()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(meal);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Delete.Url($"/mealplans/children/{child.Id}/meals/{meal.Id}");
            _.StatusCodeShouldBe(204);
        });

        var listResponse = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/mealplans/children/{child.Id}/meals");
            _.StatusCodeShouldBeOk();
        });

        var listed = Assert.Single(listResponse.ReadAsJson<List<MealDto>>(), m => m.Id == meal.Id);
        Assert.True(listed.IsArchived);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { MealId = meal.Id, Notes = (string?)null })
                .ToUrl($"/mealplans/children/{child.Id}/plan")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "Dinner");
            _.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task The_child_cannot_archive_their_own_meal()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(meal);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Delete.Url($"/mealplans/children/{child.Id}/meals/{meal.Id}");
            _.StatusCodeShouldBe(403);
        });
    }
}
