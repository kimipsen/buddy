using Alba;

using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.Mealplans;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Mealplans.GetMealPlanIcalFeed;

[Collection(BuddyApiCollection.Name)]
public sealed class GetMealPlanIcalFeedTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("GetMealPlanIcalFeed")]
    public async Task An_anonymous_request_with_a_valid_token_returns_the_ics_feed_using_the_default_slot_time()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id, new CreateMealOptions(Name: "Pancakes"));
        Assert.NotNull(meal);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { MealId = meal.Id, Notes = (string?)null })
                .ToUrl($"/mealplans/children/{child.Id}/plan")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "Breakfast");
            _.StatusCodeShouldBeOk();
        });

        var issued = await MealplanTestHelpers.CreateIcalTokenAsync(fixture, guardianToken, child.Id);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.Get.Url(issued.SubscriptionPath);
            _.StatusCodeShouldBeOk();
            _.ContentTypeShouldBe("text/calendar");
        });

        var ics = response.ReadAsText();
        Assert.Contains("Breakfast: Pancakes", ics);
        // 07:00 is MealSlotDefaultTimes' built-in Breakfast default -- no slot time was configured.
        Assert.Contains($"DTSTART:{today:yyyyMMdd}T070000", ics);
        // Floating local time, not anchored to UTC -- no trailing Z or TZID.
        Assert.DoesNotContain($"DTSTART:{today:yyyyMMdd}T070000Z", ics);
    }

    [Fact]
    public async Task An_unconfigured_snack_slot_still_renders_using_its_built_in_default()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id, new CreateMealOptions(Name: "Apple Slices"));
        Assert.NotNull(meal);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { MealId = meal.Id, Notes = (string?)null })
                .ToUrl($"/mealplans/children/{child.Id}/plan")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "Snack");
            _.StatusCodeShouldBeOk();
        });

        var issued = await MealplanTestHelpers.CreateIcalTokenAsync(fixture, guardianToken, child.Id);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.Get.Url(issued.SubscriptionPath);
            _.StatusCodeShouldBeOk();
        });

        var ics = response.ReadAsText();
        Assert.Contains("Snack: Apple Slices", ics);
        Assert.Contains($"DTSTART:{today:yyyyMMdd}T150000", ics);
    }

    [Fact]
    public async Task An_invalid_token_returns_not_found()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var issued = await MealplanTestHelpers.CreateIcalTokenAsync(fixture, guardianToken, child.Id);
        var mealPlanId = issued.SubscriptionPath.Split('/')[2];

        await fixture.Host.Scenario(_ =>
        {
            _.Get.Url($"/mealplans/{mealPlanId}/ical/not-a-real-token");
            _.StatusCodeShouldBe(404);
        });
    }
}
