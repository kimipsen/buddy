using Alba;

using buddy.Common;
using buddy.Features.Mealplans;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Mealplans.AiAssistant;

// GDPR Question 6.3 (docs/backend/analysis/gdpr-data-protection.md).
[Collection(BuddyApiCollection.Name)]
public sealed class AcknowledgeAiDataSharingTests(BuddyApiFixture fixture)
{
    private static readonly DateOnly From = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

    [Fact]
    public async Task A_session_cannot_start_before_data_sharing_is_acknowledged()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        await AiAssistantTestHelpers.ConfigureAnthropicKeyAsync(fixture, guardianToken, child.Id, acknowledgeDataSharing: false);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new { From, To = From, Slots = new[] { MealSlot.Dinner }, MustIncludeMealIds = Array.Empty<Guid>() })
                .ToUrl($"/mealplans/children/{child.Id}/ai/sessions");
            _.StatusCodeShouldBe(409);
        });

        Assert.Equal(StartAiSessionOutcome.DataSharingNotAcknowledgedCode, response.ReadAsJson<ErrorEnvelope>().Code);
    }

    [Fact]
    [CoversEndpoint("AcknowledgeAiDataSharing")]
    public async Task Once_acknowledged_a_session_can_start()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        await AiAssistantTestHelpers.ConfigureAnthropicKeyAsync(fixture, guardianToken, child.Id, acknowledgeDataSharing: false);

        var settings = await AiAssistantTestHelpers.AcknowledgeDataSharingAsync(fixture, guardianToken, child.Id);

        Assert.NotNull(settings.DataSharingAcknowledgedAt);
        await AiAssistantTestHelpers.StartSessionAsync(fixture, guardianToken, child.Id, From, From, [MealSlot.Dinner]);
    }

    [Fact]
    public async Task Acknowledging_again_keeps_the_first_acknowledgement()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        await AiAssistantTestHelpers.ConfigureAnthropicKeyAsync(fixture, guardianToken, child.Id, acknowledgeDataSharing: false);

        var first = await AiAssistantTestHelpers.AcknowledgeDataSharingAsync(fixture, guardianToken, child.Id);
        var second = await AiAssistantTestHelpers.AcknowledgeDataSharingAsync(fixture, guardianToken, child.Id);

        Assert.Equal(first.DataSharingAcknowledgedAt, second.DataSharingAcknowledgedAt);
    }

    [Fact]
    public async Task The_acknowledgement_covers_the_whole_family()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var sibling = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Robin");
        await AiAssistantTestHelpers.ConfigureAnthropicKeyAsync(fixture, guardianToken, child.Id);

        await AiAssistantTestHelpers.StartSessionAsync(fixture, guardianToken, sibling.Id, From, From, [MealSlot.Dinner]);
    }

    [Fact]
    public async Task The_provider_settings_show_whether_data_sharing_is_acknowledged()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        await AiAssistantTestHelpers.ConfigureAnthropicKeyAsync(fixture, guardianToken, child.Id, acknowledgeDataSharing: false);

        Assert.Null((await GetSettingsAsync(guardianToken, child.Id)).DataSharingAcknowledgedAt);

        await AiAssistantTestHelpers.AcknowledgeDataSharingAsync(fixture, guardianToken, child.Id);

        Assert.NotNull((await GetSettingsAsync(guardianToken, child.Id)).DataSharingAcknowledgedAt);
    }

    [Fact]
    public async Task Acknowledging_without_a_configured_provider_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        await AiAssistantTestHelpers.AcknowledgeDataSharingAsync(fixture, guardianToken, child.Id, expectedStatus: 400);
    }

    [Fact]
    public async Task A_child_cannot_acknowledge_data_sharing()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        await AiAssistantTestHelpers.ConfigureAnthropicKeyAsync(fixture, guardianToken, child.Id, acknowledgeDataSharing: false);
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        await AiAssistantTestHelpers.AcknowledgeDataSharingAsync(fixture, childToken, child.Id, expectedStatus: 403);
    }

    [Fact]
    public async Task An_unrelated_user_cannot_acknowledge_data_sharing()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        await AiAssistantTestHelpers.ConfigureAnthropicKeyAsync(fixture, guardianToken, child.Id, acknowledgeDataSharing: false);

        await AiAssistantTestHelpers.AcknowledgeDataSharingAsync(fixture, strangerToken, child.Id, expectedStatus: 404);
    }

    private async Task<AiProviderSettingsDto> GetSettingsAsync(string token, Guid childId)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"/mealplans/children/{childId}/ai/providers");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<AiProviderSettingsDto>();
    }
}
