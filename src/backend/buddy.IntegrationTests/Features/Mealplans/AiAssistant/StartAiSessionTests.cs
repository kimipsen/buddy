using Alba;

using buddy.Common;
using buddy.Features.Mealplans;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Mealplans.AiAssistant;

[Collection(BuddyApiCollection.Name)]
public sealed class StartAiSessionTests(BuddyApiFixture fixture)
{
    private static readonly DateOnly From = DateOnly.FromDateTime(DateTime.UtcNow);
    private static readonly DateOnly To = From.AddDays(2);

    [Fact]
    public async Task Starting_a_session_without_a_configured_provider_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new { From, To, Slots = new[] { MealSlot.Dinner }, MustIncludeMealIds = Array.Empty<Guid>(), Notes = (string?)null })
                .ToUrl($"/mealplans/children/{child.Id}/ai/sessions");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
    }

    [Fact]
    [CoversEndpoint("StartAiSession")]
    public async Task A_guardian_can_start_a_session_once_a_provider_is_configured()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        await AiAssistantTestHelpers.ConfigureAnthropicKeyAsync(fixture, guardianToken, child.Id);

        var view = await AiAssistantTestHelpers.StartSessionAsync(fixture, guardianToken, child.Id, From, To, [MealSlot.Dinner, MealSlot.Lunch]);

        Assert.Equal(From, view.From);
        Assert.Equal(To, view.To);
        Assert.Equal(AiSessionStatus.Drafting, view.Status);
        Assert.Empty(view.Transcript);
        Assert.Empty(view.Draft);
        Assert.Equal(new[] { MealSlot.Dinner, MealSlot.Lunch }.OrderBy(s => s), view.RequestedSlots.OrderBy(s => s));
    }

    [Fact]
    public async Task Starting_a_new_session_replaces_the_current_one()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        await AiAssistantTestHelpers.ConfigureAnthropicKeyAsync(fixture, guardianToken, child.Id);

        await AiAssistantTestHelpers.StartSessionAsync(fixture, guardianToken, child.Id, From, To, [MealSlot.Dinner]);

        var second = await AiAssistantTestHelpers.StartSessionAsync(fixture, guardianToken, child.Id, From.AddDays(10), From.AddDays(12), [MealSlot.Breakfast]);

        Assert.Equal(From.AddDays(10), second.From);
        Assert.Equal(From.AddDays(12), second.To);
        Assert.Equal([MealSlot.Breakfast], second.RequestedSlots);
    }

    [Fact]
    public async Task A_range_over_the_maximum_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        await AiAssistantTestHelpers.ConfigureAnthropicKeyAsync(fixture, guardianToken, child.Id);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new { From, To = From.AddDays(StartAiSessionValidator.MaxRangeDays + 1), Slots = new[] { MealSlot.Dinner }, MustIncludeMealIds = Array.Empty<Guid>(), Notes = (string?)null })
                .ToUrl($"/mealplans/children/{child.Id}/ai/sessions");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Equal([$"The requested range cannot exceed {StartAiSessionValidator.MaxRangeDays} days."], error.Details["To"]);
    }

    [Fact]
    public async Task Requesting_no_slots_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        await AiAssistantTestHelpers.ConfigureAnthropicKeyAsync(fixture, guardianToken, child.Id);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new { From, To, Slots = Array.Empty<MealSlot>(), MustIncludeMealIds = Array.Empty<Guid>(), Notes = (string?)null })
                .ToUrl($"/mealplans/children/{child.Id}/ai/sessions");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Equal(["At least one meal slot must be requested."], error.Details["RequestedSlots"]);
    }

    [Fact]
    public async Task A_child_cannot_start_a_session()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        await AiAssistantTestHelpers.ConfigureAnthropicKeyAsync(fixture, guardianToken, child.Id);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Post.Json(new { From, To, Slots = new[] { MealSlot.Dinner }, MustIncludeMealIds = Array.Empty<Guid>(), Notes = (string?)null })
                .ToUrl($"/mealplans/children/{child.Id}/ai/sessions");
            _.StatusCodeShouldBe(403);
        });
    }

    [Fact]
    public async Task A_range_of_exactly_the_maximum_is_accepted()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        await AiAssistantTestHelpers.ConfigureAnthropicKeyAsync(fixture, guardianToken, child.Id);
        var to = From.AddDays(StartAiSessionValidator.MaxRangeDays);

        var view = await AiAssistantTestHelpers.StartSessionAsync(fixture, guardianToken, child.Id, From, to, [MealSlot.Dinner]);

        Assert.Equal(From, view.From);
        Assert.Equal(to, view.To);
    }

    [Fact]
    public async Task A_range_where_to_is_before_from_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        await AiAssistantTestHelpers.ConfigureAnthropicKeyAsync(fixture, guardianToken, child.Id);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new { From, To = From.AddDays(-1), Slots = new[] { MealSlot.Dinner }, MustIncludeMealIds = Array.Empty<Guid>(), Notes = (string?)null })
                .ToUrl($"/mealplans/children/{child.Id}/ai/sessions");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Equal(["'to' must not be before 'from'."], error.Details["To"]);
    }

    [Fact]
    public async Task A_single_day_range_is_accepted()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        await AiAssistantTestHelpers.ConfigureAnthropicKeyAsync(fixture, guardianToken, child.Id);

        var view = await AiAssistantTestHelpers.StartSessionAsync(fixture, guardianToken, child.Id, From, From, [MealSlot.Dinner]);

        Assert.Equal(From, view.From);
        Assert.Equal(From, view.To);
    }

    [Fact]
    public async Task Notes_of_exactly_2000_characters_are_accepted()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        await AiAssistantTestHelpers.ConfigureAnthropicKeyAsync(fixture, guardianToken, child.Id);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new { From, To, Slots = new[] { MealSlot.Dinner }, MustIncludeMealIds = Array.Empty<Guid>(), Notes = new string('n', 2000) })
                .ToUrl($"/mealplans/children/{child.Id}/ai/sessions");
            _.StatusCodeShouldBeOk();
        });
    }

    [Fact]
    public async Task Notes_longer_than_2000_characters_are_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        await AiAssistantTestHelpers.ConfigureAnthropicKeyAsync(fixture, guardianToken, child.Id);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new { From, To, Slots = new[] { MealSlot.Dinner }, MustIncludeMealIds = Array.Empty<Guid>(), Notes = new string('n', 2001) })
                .ToUrl($"/mealplans/children/{child.Id}/ai/sessions");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Contains("Notes", error.Details.Keys);
    }
}
