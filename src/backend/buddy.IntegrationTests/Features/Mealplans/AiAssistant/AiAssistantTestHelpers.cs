using Alba;

using buddy.Features.Mealplans;
using buddy.IntegrationTests.Fixtures;

namespace buddy.IntegrationTests.Features.Mealplans.AiAssistant;

internal static class AiAssistantTestHelpers
{
    public static async Task ConfigureAnthropicKeyAsync(BuddyApiFixture fixture, string guardianToken, Guid childId, string apiKey = "sk-ant-test1234")
    {
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { ApiKey = apiKey }).ToUrl($"/mealplans/children/{childId}/ai/providers/Anthropic/key");
            _.StatusCodeShouldBeOk();
        });
    }

    public static async Task<AiSessionViewDto> StartSessionAsync(
        BuddyApiFixture fixture, string guardianToken, Guid childId, DateOnly from, DateOnly to, IReadOnlyCollection<MealSlot> slots, int expectedStatus = 200)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new { From = from, To = to, Slots = slots, MustIncludeMealIds = Array.Empty<Guid>(), Notes = (string?)null })
                .ToUrl($"/mealplans/children/{childId}/ai/sessions");
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<AiSessionViewDto>() : null!;
    }
}
