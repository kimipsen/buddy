using Alba;

using buddy.Common;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Mealplans.AiAssistant;

[Collection(BuddyApiCollection.Name)]
public sealed class TestProviderConnectionTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("TestProviderConnection")]
    public async Task An_invalid_key_reports_a_failed_connection()
    {
        // A real call to Anthropic with a deliberately-bad key -- exercises the actual HTTP
        // request/response handling (headers, error-body parsing) without needing a real paid key
        // or a fake provider standing in for it.
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new { ApiKey = "sk-ant-definitely-invalid-test-key" }).ToUrl($"/mealplans/children/{child.Id}/ai/providers/Anthropic/test-connection");
            _.StatusCodeShouldBeOk();
        });

        var result = response.ReadAsJson<TestProviderConnectionResultDto>();
        Assert.False(result.IsSuccessful);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public async Task An_invalid_OpenAi_key_reports_a_failed_connection()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new { ApiKey = "sk-definitely-invalid-test-key" }).ToUrl($"/mealplans/children/{child.Id}/ai/providers/OpenAi/test-connection");
            _.StatusCodeShouldBeOk();
        });

        var result = response.ReadAsJson<TestProviderConnectionResultDto>();
        Assert.False(result.IsSuccessful);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public async Task An_invalid_Gemini_key_reports_a_failed_connection()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new { ApiKey = "definitely-invalid-test-key" }).ToUrl($"/mealplans/children/{child.Id}/ai/providers/Gemini/test-connection");
            _.StatusCodeShouldBeOk();
        });

        var result = response.ReadAsJson<TestProviderConnectionResultDto>();
        Assert.False(result.IsSuccessful);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public async Task Testing_a_provider_with_no_stored_or_provided_key_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new { ApiKey = (string?)null }).ToUrl($"/mealplans/children/{child.Id}/ai/providers/Anthropic/test-connection");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
    }

    [Fact]
    public async Task A_child_cannot_test_a_provider_connection()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Post.Json(new { ApiKey = "sk-ant-whatever" }).ToUrl($"/mealplans/children/{child.Id}/ai/providers/Anthropic/test-connection");
            _.StatusCodeShouldBe(403);
        });
    }

    [Fact]
    public async Task A_key_of_exactly_500_characters_passes_validation()
    {
        // The validator runs before the guardian check, so a 404 for a child the caller has no link
        // to proves the 500-character key was accepted -- without sending it to a real provider.
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new { ApiKey = new string('k', 500) }).ToUrl($"/mealplans/children/{Guid.NewGuid()}/ai/providers/Anthropic/test-connection");
            _.StatusCodeShouldBe(404);
        });
    }

    [Fact]
    public async Task A_key_longer_than_500_characters_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new { ApiKey = new string('k', 501) }).ToUrl($"/mealplans/children/{child.Id}/ai/providers/Anthropic/test-connection");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Contains("ApiKey", error.Details.Keys);
    }
}
