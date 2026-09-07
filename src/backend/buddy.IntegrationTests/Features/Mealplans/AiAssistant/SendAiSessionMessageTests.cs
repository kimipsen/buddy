using Alba;

using buddy.Common;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Mealplans.AiAssistant;

// The happy path (a real tool-calling round trip against a live provider) isn't covered here -- it
// needs a working provider API key, which these tests deliberately don't depend on. These cover
// every failure path the handler resolves before it ever calls a provider.
[Collection(BuddyApiCollection.Name)]
public sealed class SendAiSessionMessageTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("SendAiSessionMessage")]
    public async Task Sending_a_message_without_an_active_session_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        await AiAssistantTestHelpers.ConfigureAnthropicKeyAsync(fixture, guardianToken, child.Id);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new { Text = "Plan five dinners for us." }).ToUrl($"/mealplans/children/{child.Id}/ai/sessions/current/messages");
            _.StatusCodeShouldBe(404);
        });
    }

    [Fact]
    public async Task An_empty_message_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new { Text = "" }).ToUrl($"/mealplans/children/{child.Id}/ai/sessions/current/messages");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
    }

    [Fact]
    public async Task A_child_cannot_send_a_session_message()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Post.Json(new { Text = "Plan five dinners for us." }).ToUrl($"/mealplans/children/{child.Id}/ai/sessions/current/messages");
            _.StatusCodeShouldBe(403);
        });
    }
}
