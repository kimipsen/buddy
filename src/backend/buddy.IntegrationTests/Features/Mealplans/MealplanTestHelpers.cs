using Alba;

using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Fixtures;

namespace buddy.IntegrationTests.Features.Mealplans;

internal sealed record CreateMealOptions(string Name = "Tacos", string? Description = "Ground beef, tortillas, salsa");

internal static class MealplanTestHelpers
{
    public static async Task<MealDto?> CreateMealAsync(
        BuddyApiFixture fixture, string guardianToken, Guid childId, CreateMealOptions? options = null, int expectedStatus = 200)
    {
        options ??= new CreateMealOptions();

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new { options.Name, options.Description, Icon = "taco", Color = "#ffaa00" })
                .ToUrl($"/mealplans/children/{childId}/meals");
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<MealDto>() : null;
    }

    // Creates a group owned by the guardian and shares the child's plan with it, so the guardian
    // reaches the plan through the /mealplans/groups/{groupId}/... routes with Manage tier (the
    // default policy grants it to the Owner).
    public static async Task<Guid> ShareWithNewGroupAsync(BuddyApiFixture fixture, string guardianToken, Guid childId)
    {
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, guardianToken, "Co-parents");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Url($"/mealplans/children/{childId}/plan/groups/{groupId}");
            _.StatusCodeShouldBe(204);
        });

        return groupId;
    }

    public static async Task<MealPlanIcalTokenResponseDto> CreateIcalTokenAsync(BuddyApiFixture fixture, string guardianToken, Guid childId)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Url($"/mealplans/children/{childId}/ical-tokens");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<MealPlanIcalTokenResponseDto>();
    }
}
