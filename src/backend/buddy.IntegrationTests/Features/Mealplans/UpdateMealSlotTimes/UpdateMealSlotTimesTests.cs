using Alba;

using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.Mealplans;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Mealplans.UpdateMealSlotTimes;

[Collection(BuddyApiCollection.Name)]
public sealed class UpdateMealSlotTimesTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("UpdateMealSlotTimes")]
    public async Task A_guardian_can_configure_a_slot_time_and_it_shows_up_in_the_feed()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id, new CreateMealOptions(Name: "Lasagna"));
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

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { Times = new Dictionary<string, string> { ["Dinner"] = "19:30:00" } })
                .ToUrl($"/mealplans/children/{child.Id}/slot-times");
            _.StatusCodeShouldBe(204);
        });

        var issued = await MealplanTestHelpers.CreateIcalTokenAsync(fixture, guardianToken, child.Id);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.Get.Url(issued.SubscriptionPath);
            _.StatusCodeShouldBeOk();
        });

        var ics = response.ReadAsText();
        Assert.Contains($"DTSTART:{today:yyyyMMdd}T193000", ics);
    }

    // Exercises the "plan already exists" branch of the handler (the first call above only ever
    // hits the "create a new plan" branch), specifically the changed-vs-unchanged diffing that
    // decides what gets appended -- SonarCloud's symbolic execution flagged that branch's
    // `changes.Count > 0` as unreachable; this proves at runtime that it isn't.
    [Fact]
    [CoversEndpoint("UpdateMealSlotTimes")]
    public async Task Reconfiguring_an_existing_plans_slot_times_updates_the_feed()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id, new CreateMealOptions(Name: "Lasagna"));
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

        // First call creates the plan (Dinner: 19:30).
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { Times = new Dictionary<string, string> { ["Dinner"] = "19:30:00" } })
                .ToUrl($"/mealplans/children/{child.Id}/slot-times");
            _.StatusCodeShouldBe(204);
        });

        // Second call hits the existing-plan branch: Dinner changes (19:30 -> 20:00) and
        // Breakfast is newly added -- both should produce a MealPlanSlotTimeSet change.
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { Times = new Dictionary<string, string> { ["Dinner"] = "20:00:00", ["Breakfast"] = "07:00:00" } })
                .ToUrl($"/mealplans/children/{child.Id}/slot-times");
            _.StatusCodeShouldBe(204);
        });

        var issued = await MealplanTestHelpers.CreateIcalTokenAsync(fixture, guardianToken, child.Id);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.Get.Url(issued.SubscriptionPath);
            _.StatusCodeShouldBeOk();
        });

        var ics = response.ReadAsText();
        Assert.Contains($"DTSTART:{today:yyyyMMdd}T200000", ics);
        Assert.DoesNotContain($"DTSTART:{today:yyyyMMdd}T193000", ics);
    }

    [Fact]
    public async Task The_child_cannot_configure_slot_times()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Put.Json(new { Times = new Dictionary<string, string> { ["Breakfast"] = "07:30:00" } })
                .ToUrl($"/mealplans/children/{child.Id}/slot-times");
            _.StatusCodeShouldBe(403);
        });
    }
}
