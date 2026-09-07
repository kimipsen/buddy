using Alba;

using buddy.Features.Mealplans;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Mealplans.AiAssistant;

[Collection(BuddyApiCollection.Name)]
public sealed class DiscardAiSessionTests(BuddyApiFixture fixture)
{
    private static readonly DateOnly From = DateOnly.FromDateTime(DateTime.UtcNow);
    private static readonly DateOnly To = From.AddDays(2);

    [Fact]
    public async Task Discarding_with_no_session_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Url($"/mealplans/children/{child.Id}/ai/sessions/current/discard");
            _.StatusCodeShouldBe(404);
        });
    }

    [Fact]
    [CoversEndpoint("DiscardAiSession")]
    public async Task A_guardian_can_discard_a_drafting_session()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        await AiAssistantTestHelpers.ConfigureAnthropicKeyAsync(fixture, guardianToken, child.Id);
        await AiAssistantTestHelpers.StartSessionAsync(fixture, guardianToken, child.Id, From, To, [MealSlot.Dinner]);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Url($"/mealplans/children/{child.Id}/ai/sessions/current/discard");
            _.StatusCodeShouldBeOk();
        });

        var view = response.ReadAsJson<AiSessionViewDto>();
        Assert.Equal(AiSessionStatus.Discarded, view.Status);
    }

    [Fact]
    public async Task Discarding_an_already_discarded_session_is_idempotent()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        await AiAssistantTestHelpers.ConfigureAnthropicKeyAsync(fixture, guardianToken, child.Id);
        await AiAssistantTestHelpers.StartSessionAsync(fixture, guardianToken, child.Id, From, To, [MealSlot.Dinner]);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Url($"/mealplans/children/{child.Id}/ai/sessions/current/discard");
            _.StatusCodeShouldBeOk();
        });

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Url($"/mealplans/children/{child.Id}/ai/sessions/current/discard");
            _.StatusCodeShouldBeOk();
        });

        Assert.Equal(AiSessionStatus.Discarded, response.ReadAsJson<AiSessionViewDto>().Status);
    }

    [Fact]
    public async Task A_child_cannot_discard_a_session()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Post.Url($"/mealplans/children/{child.Id}/ai/sessions/current/discard");
            _.StatusCodeShouldBe(403);
        });
    }
}
