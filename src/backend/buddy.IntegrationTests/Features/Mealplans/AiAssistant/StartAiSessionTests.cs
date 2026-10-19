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
        Assert.Equal([$"The requested range cannot exceed {StartAiSessionValidator.MaxRangeDays} days."], error.Details["to"]);
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
        Assert.Equal(["At least one meal slot must be requested."], error.Details["slots"]);
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
        Assert.Equal(["'to' must not be before 'from'."], error.Details["to"]);
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
        Assert.Contains("notes", error.Details.Keys);
    }

    [Fact]
    public async Task A_session_without_a_filter_reports_no_filter()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        await AiAssistantTestHelpers.ConfigureAnthropicKeyAsync(fixture, guardianToken, child.Id);

        var view = await AiAssistantTestHelpers.StartSessionAsync(fixture, guardianToken, child.Id, From, To, [MealSlot.Dinner]);

        Assert.False(view.RatedOnly);
        Assert.Equal(AiServedWindow.Any, view.ServedWithin);
    }

    [Fact]
    public async Task A_filtered_session_starts_when_a_meal_was_served_in_the_window_and_echoes_the_filter()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        await AiAssistantTestHelpers.ConfigureAnthropicKeyAsync(fixture, guardianToken, child.Id);
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(meal);
        await AiAssistantTestHelpers.AssignDinnerAsync(fixture, guardianToken, child.Id, meal.Id, From.AddDays(-5));

        var view = await AiAssistantTestHelpers.StartSessionAsync(
            fixture, guardianToken, child.Id, From, To, [MealSlot.Dinner], servedWithin: AiServedWindow.Last30Days);

        Assert.False(view.RatedOnly);
        Assert.Equal(AiServedWindow.Last30Days, view.ServedWithin);
    }

    [Fact]
    public async Task A_filter_that_matches_no_meals_is_rejected_and_keeps_the_current_session()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        await AiAssistantTestHelpers.ConfigureAnthropicKeyAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id));
        var current = await AiAssistantTestHelpers.StartSessionAsync(fixture, guardianToken, child.Id, From, To, [MealSlot.Dinner]);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new { From, To, Slots = new[] { MealSlot.Dinner }, MustIncludeMealIds = Array.Empty<Guid>(), RatedOnly = true })
                .ToUrl($"/mealplans/children/{child.Id}/ai/sessions");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Equal([StartAiSessionHandler.NoMatchingMealsMessage], error.Details["servedWithin"]);

        var stillCurrent = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/mealplans/children/{child.Id}/ai/sessions/current");
            _.StatusCodeShouldBeOk();
        });
        var currentView = stillCurrent.ReadAsJson<AiSessionViewDto>();
        Assert.Equal(current.Id, currentView.Id);
        Assert.Equal(AiSessionStatus.Drafting, currentView.Status);
    }

    [Fact]
    public async Task A_served_window_that_is_not_30_60_or_90_days_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        await AiAssistantTestHelpers.ConfigureAnthropicKeyAsync(fixture, guardianToken, child.Id);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new { From, To, Slots = new[] { MealSlot.Dinner }, MustIncludeMealIds = Array.Empty<Guid>(), ServedWithin = 45 })
                .ToUrl($"/mealplans/children/{child.Id}/ai/sessions");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Contains("servedWithin", error.Details.Keys);
    }

    [Fact]
    public async Task Asking_the_assistant_to_include_an_eating_out_meal_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        await AiAssistantTestHelpers.ConfigureAnthropicKeyAsync(fixture, guardianToken, child.Id);
        var restaurant = await MealplanTestHelpers.CreateMealAsync(
            fixture, guardianToken, child.Id, new CreateMealOptions(Name: "Restaurant", EatingOut: MealEatingOut.Restaurant));
        Assert.NotNull(restaurant);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new { From, To, Slots = new[] { MealSlot.Dinner }, MustIncludeMealIds = new[] { restaurant.Id } })
                .ToUrl($"/mealplans/children/{child.Id}/ai/sessions");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Equal([StartAiSessionHandler.EatingOutMustIncludeMessage], error.Details["mustIncludeMealIds"]);
    }

    // Another family's eating-out meal is treated like an unknown id (accepted, never offered to the
    // provider), so the answer doesn't reveal that the id is a meal, let alone an eating-out one.
    [Fact]
    public async Task Another_familys_eating_out_meal_is_treated_like_an_unknown_meal_id()
    {
        var (_, otherGuardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var otherChild = await GuardianTestHelpers.CreateChildAsync(fixture, otherGuardianToken, "Robin");
        var otherRestaurant = await MealplanTestHelpers.CreateMealAsync(
            fixture, otherGuardianToken, otherChild.Id, new CreateMealOptions(Name: "Restaurant", EatingOut: MealEatingOut.Restaurant));
        Assert.NotNull(otherRestaurant);

        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        await AiAssistantTestHelpers.ConfigureAnthropicKeyAsync(fixture, guardianToken, child.Id);

        foreach (var mealId in new[] { otherRestaurant.Id, Guid.NewGuid() })
        {
            await fixture.Host.Scenario(_ =>
            {
                _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
                _.Post.Json(new { From, To, Slots = new[] { MealSlot.Dinner }, MustIncludeMealIds = new[] { mealId } })
                    .ToUrl($"/mealplans/children/{child.Id}/ai/sessions");
                _.StatusCodeShouldBeOk();
            });
        }
    }
}
