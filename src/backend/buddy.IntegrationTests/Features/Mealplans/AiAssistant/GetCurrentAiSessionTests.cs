using Alba;

using buddy.Features.Mealplans;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Mealplans.AiAssistant;

[Collection(BuddyApiCollection.Name)]
public sealed class GetCurrentAiSessionTests(BuddyApiFixture fixture)
{
    private static readonly DateOnly From = DateOnly.FromDateTime(DateTime.UtcNow);
    private static readonly DateOnly To = From.AddDays(2);

    [Fact]
    public async Task Getting_the_current_session_with_none_started_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/mealplans/children/{child.Id}/ai/sessions/current");
            _.StatusCodeShouldBe(404);
        });
    }

    [Fact]
    [CoversEndpoint("GetCurrentAiSession")]
    public async Task A_guardian_can_fetch_the_session_they_just_started()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        await AiAssistantTestHelpers.ConfigureAnthropicKeyAsync(fixture, guardianToken, child.Id);
        var started = await AiAssistantTestHelpers.StartSessionAsync(fixture, guardianToken, child.Id, From, To, [MealSlot.Dinner]);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/mealplans/children/{child.Id}/ai/sessions/current");
            _.StatusCodeShouldBeOk();
        });

        var view = response.ReadAsJson<AiSessionViewDto>();
        Assert.Equal(started.Id, view.Id);
        Assert.Equal(AiSessionStatus.Drafting, view.Status);
    }

    [Fact]
    public async Task A_child_cannot_fetch_the_current_session()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Get.Url($"/mealplans/children/{child.Id}/ai/sessions/current");
            _.StatusCodeShouldBe(403);
        });
    }
}
