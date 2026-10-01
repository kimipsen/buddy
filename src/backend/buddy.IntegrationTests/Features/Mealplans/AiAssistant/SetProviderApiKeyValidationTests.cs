using Alba;

using buddy.Common;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;

using Xunit;

namespace buddy.IntegrationTests.Features.Mealplans.AiAssistant;

// SetProviderApiKeyValidator's rules, one per test. The rest of the SetProviderApiKey behaviour
// (activation, switching, removal) lives in AiProviderCredentialsTests.
[Collection(BuddyApiCollection.Name)]
public sealed class SetProviderApiKeyValidationTests(BuddyApiFixture fixture)
{
    [Fact]
    public async Task An_undefined_provider_is_rejected()
    {
        // Minimal APIs bind the {provider} route value with Enum.TryParse, which accepts any
        // numeric string -- so "99" reaches the validator as an undefined AiProvider value.
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { ApiKey = "sk-ant-test1234" }).ToUrl($"/mealplans/children/{child.Id}/ai/providers/99/key");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Contains("Provider", error.Details.Keys);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_api_key_is_rejected(string apiKey)
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { ApiKey = apiKey }).ToUrl($"/mealplans/children/{child.Id}/ai/providers/Anthropic/key");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Contains("ApiKey", error.Details.Keys);
    }

    [Fact]
    public async Task An_api_key_of_exactly_500_characters_is_accepted()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var apiKey = new string('k', 496) + "WXYZ";

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { ApiKey = apiKey }).ToUrl($"/mealplans/children/{child.Id}/ai/providers/Anthropic/key");
            _.StatusCodeShouldBeOk();
        });

        var entry = Assert.Single(response.ReadAsJson<AiProviderSettingsDto>().Providers);
        Assert.Equal("WXYZ", entry.Last4);
    }

    [Fact]
    public async Task An_api_key_longer_than_500_characters_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { ApiKey = new string('k', 501) }).ToUrl($"/mealplans/children/{child.Id}/ai/providers/Anthropic/key");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Contains("ApiKey", error.Details.Keys);
    }
}
