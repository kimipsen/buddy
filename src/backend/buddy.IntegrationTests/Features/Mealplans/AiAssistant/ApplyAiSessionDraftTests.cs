using Alba;

using buddy.Common;
using buddy.Features.Mealplans;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Mealplans.AiAssistant;

[Collection(BuddyApiCollection.Name)]
public sealed class ApplyAiSessionDraftTests(BuddyApiFixture fixture)
{
    private static readonly DateOnly From = DateOnly.FromDateTime(DateTime.UtcNow);
    private static readonly DateOnly To = From.AddDays(2);

    [Fact]
    public async Task Applying_with_no_session_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Url($"/mealplans/children/{child.Id}/ai/sessions/current/apply");
            _.StatusCodeShouldBe(404);
        });
    }

    [Fact]
    [CoversEndpoint("ApplyAiSessionDraft")]
    public async Task Applying_an_empty_draft_still_marks_the_session_applied()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        await AiAssistantTestHelpers.ConfigureAnthropicKeyAsync(fixture, guardianToken, child.Id);
        await AiAssistantTestHelpers.StartSessionAsync(fixture, guardianToken, child.Id, From, To, [MealSlot.Dinner]);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Url($"/mealplans/children/{child.Id}/ai/sessions/current/apply");
            _.StatusCodeShouldBeOk();
        });

        var view = response.ReadAsJson<AiSessionViewDto>();
        Assert.Equal(AiSessionStatus.Applied, view.Status);
        Assert.Empty(view.Draft);
    }

    [Fact]
    public async Task Applying_an_already_applied_session_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        await AiAssistantTestHelpers.ConfigureAnthropicKeyAsync(fixture, guardianToken, child.Id);
        await AiAssistantTestHelpers.StartSessionAsync(fixture, guardianToken, child.Id, From, To, [MealSlot.Dinner]);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Url($"/mealplans/children/{child.Id}/ai/sessions/current/apply");
            _.StatusCodeShouldBeOk();
        });

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Url($"/mealplans/children/{child.Id}/ai/sessions/current/apply");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
    }

    [Fact]
    public async Task A_child_cannot_apply_a_draft()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Post.Url($"/mealplans/children/{child.Id}/ai/sessions/current/apply");
            _.StatusCodeShouldBe(403);
        });
    }
}
