using Alba;

using buddy.Features.Mealplans;
using buddy.IntegrationTests.Fixtures;

namespace buddy.IntegrationTests.Features.Mealplans.AiAssistant;

internal static class AiAssistantTestHelpers
{
    // Also acknowledges the data-sharing notice by default, since StartAiSession refuses without it.
    public static async Task ConfigureAnthropicKeyAsync(
        BuddyApiFixture fixture, string guardianToken, Guid childId, string apiKey = "sk-ant-test1234", bool acknowledgeDataSharing = true)
    {
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { ApiKey = apiKey }).ToUrl($"/mealplans/children/{childId}/ai/providers/Anthropic/key");
            _.StatusCodeShouldBeOk();
        });

        if (acknowledgeDataSharing)
        {
            await AcknowledgeDataSharingAsync(fixture, guardianToken, childId);
        }
    }

    public static async Task<AiProviderSettingsDto> AcknowledgeDataSharingAsync(BuddyApiFixture fixture, string token, Guid childId, int expectedStatus = 200)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Url($"/mealplans/children/{childId}/ai/data-sharing-acknowledgement");
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<AiProviderSettingsDto>() : null!;
    }

    public static async Task<AiSessionViewDto> StartSessionAsync(
        BuddyApiFixture fixture, string guardianToken, Guid childId, DateOnly from, DateOnly to, IReadOnlyCollection<MealSlot> slots, int expectedStatus = 200, string? notes = null)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new { From = from, To = to, Slots = slots, MustIncludeMealIds = Array.Empty<Guid>(), Notes = notes })
                .ToUrl($"/mealplans/children/{childId}/ai/sessions");
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<AiSessionViewDto>() : null!;
    }
}
