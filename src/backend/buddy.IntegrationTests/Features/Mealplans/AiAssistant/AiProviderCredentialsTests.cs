using Alba;

using buddy.Common;
using buddy.Features.Mealplans;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Mealplans.AiAssistant;

[Collection(BuddyApiCollection.Name)]
public sealed class AiProviderCredentialsTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("SetProviderApiKey")]
    [CoversEndpoint("ListProviders")]
    public async Task Adding_the_first_key_creates_credentials_and_activates_that_provider()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { ApiKey = "sk-ant-test1234" }).ToUrl($"/mealplans/children/{child.Id}/ai/providers/Anthropic/key");
            _.StatusCodeShouldBeOk();
        });

        var settings = response.ReadAsJson<AiProviderSettingsDto>();
        var entry = Assert.Single(settings.Providers);
        Assert.Equal(AiProvider.Anthropic, entry.Provider);
        Assert.Equal("1234", entry.Last4);
        Assert.Equal(AiProvider.Anthropic, settings.ActiveProvider);

        var listed = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/mealplans/children/{child.Id}/ai/providers");
            _.StatusCodeShouldBeOk();
        });

        var listedSettings = listed.ReadAsJson<AiProviderSettingsDto>();
        var listedEntry = Assert.Single(listedSettings.Providers);
        Assert.Equal(entry, listedEntry);
        Assert.Equal(settings.ActiveProvider, listedSettings.ActiveProvider);
    }

    [Fact]
    public async Task Adding_a_second_provider_does_not_change_the_active_provider()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { ApiKey = "sk-ant-test1234" }).ToUrl($"/mealplans/children/{child.Id}/ai/providers/Anthropic/key");
            _.StatusCodeShouldBeOk();
        });

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { ApiKey = "sk-oai-test5678" }).ToUrl($"/mealplans/children/{child.Id}/ai/providers/OpenAi/key");
            _.StatusCodeShouldBeOk();
        });

        var settings = response.ReadAsJson<AiProviderSettingsDto>();
        Assert.Equal(2, settings.Providers.Count);
        Assert.Equal(AiProvider.Anthropic, settings.ActiveProvider);
    }

    [Fact]
    [CoversEndpoint("SetActiveProvider")]
    public async Task A_guardian_can_switch_the_active_provider()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { ApiKey = "sk-ant-test1234" }).ToUrl($"/mealplans/children/{child.Id}/ai/providers/Anthropic/key");
            _.StatusCodeShouldBeOk();
        });
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { ApiKey = "sk-oai-test5678" }).ToUrl($"/mealplans/children/{child.Id}/ai/providers/OpenAi/key");
            _.StatusCodeShouldBeOk();
        });

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Url($"/mealplans/children/{child.Id}/ai/active-provider/OpenAi");
            _.StatusCodeShouldBeOk();
        });

        Assert.Equal(AiProvider.OpenAi, response.ReadAsJson<AiProviderSettingsDto>().ActiveProvider);
    }

    [Fact]
    public async Task Switching_to_a_provider_with_no_stored_key_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { ApiKey = "sk-ant-test1234" }).ToUrl($"/mealplans/children/{child.Id}/ai/providers/Anthropic/key");
            _.StatusCodeShouldBeOk();
        });

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Url($"/mealplans/children/{child.Id}/ai/active-provider/Gemini");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
    }

    [Fact]
    [CoversEndpoint("RemoveProviderApiKey")]
    public async Task Removing_the_active_providers_key_clears_the_active_provider()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { ApiKey = "sk-ant-test1234" }).ToUrl($"/mealplans/children/{child.Id}/ai/providers/Anthropic/key");
            _.StatusCodeShouldBeOk();
        });

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Delete.Url($"/mealplans/children/{child.Id}/ai/providers/Anthropic/key");
            _.StatusCodeShouldBeOk();
        });

        var settings = response.ReadAsJson<AiProviderSettingsDto>();
        Assert.Empty(settings.Providers);
        Assert.Null(settings.ActiveProvider);
    }

    [Fact]
    public async Task Setting_an_empty_api_key_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { ApiKey = "" }).ToUrl($"/mealplans/children/{child.Id}/ai/providers/Anthropic/key");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
    }

    [Fact]
    public async Task A_child_cannot_manage_ai_provider_settings()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Put.Json(new { ApiKey = "sk-ant-test1234" }).ToUrl($"/mealplans/children/{child.Id}/ai/providers/Anthropic/key");
            _.StatusCodeShouldBe(403);
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Get.Url($"/mealplans/children/{child.Id}/ai/providers");
            _.StatusCodeShouldBe(403);
        });
    }
}
