using Alba;

using buddy.Common;
using buddy.Features.Mealplans;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

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

    // docs/backend/analysis/eating-out-meals.md, Question 3: a slot marked eating out after the
    // model drafted it keeps the eating-out meal; the rest of the draft still applies.
    [Fact]
    public async Task A_drafted_slot_since_marked_eating_out_is_skipped_on_apply()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        await AiAssistantTestHelpers.ConfigureAnthropicKeyAsync(fixture, guardianToken, child.Id);
        var tacos = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id);
        var grandmas = await MealplanTestHelpers.CreateMealAsync(
            fixture, guardianToken, child.Id, new CreateMealOptions(Name: "Dinner at Grandma's", EatingOut: MealEatingOut.FriendsAndFamily));
        Assert.NotNull(tacos);
        Assert.NotNull(grandmas);
        var view = await AiAssistantTestHelpers.StartSessionAsync(fixture, guardianToken, child.Id, From, To, [MealSlot.Dinner]);

        // No fake provider: the draft is written the way propose_assignment would write it.
        var sessionId = new MealplanAiSessionId(view.Id);
        await fixture.Host.Services.GetRequiredService<IAiSessionEventStore>().AppendAsync(
            sessionId,
            [
                new AiDraftAssignmentSet(sessionId, From, MealSlot.Dinner, new MealId(tacos.Id), DateTimeOffset.UtcNow),
                new AiDraftAssignmentSet(sessionId, From.AddDays(1), MealSlot.Dinner, new MealId(tacos.Id), DateTimeOffset.UtcNow)
            ],
            CancellationToken.None);
        await AiAssistantTestHelpers.AssignDinnerAsync(fixture, guardianToken, child.Id, grandmas.Id, From);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Url($"/mealplans/children/{child.Id}/ai/sessions/current/apply");
            _.StatusCodeShouldBeOk();
        });

        var plan = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/mealplans/children/{child.Id}/plan?from={From:yyyy-MM-dd}&to={To:yyyy-MM-dd}");
            _.StatusCodeShouldBeOk();
        });

        var entries = plan.ReadAsJson<List<MealPlanEntryDto>>();
        Assert.Equal([(From, grandmas.Id), (From.AddDays(1), tacos.Id)], entries.Select(e => (e.Date, e.MealId)));
    }
}
