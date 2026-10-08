using System.Collections.Immutable;
using System.Text.Json;

using buddy.Features.Calendars;
using buddy.Features.Mealplans;
using buddy.Features.Users;
using buddy.IntegrationTests.EventShapeTests;

using Xunit;

namespace buddy.IntegrationTests.Features.Mealplans.AiAssistant;

// docs/backend/analysis/ai-assistant-meal-filter.md: which meals the assistant is offered. Pure
// functions, so no fixture -- the HTTP paths are in StartAiSessionTests and SendAiSessionMessageTests.
public sealed class AiMealFilterTests
{
    private static readonly DateOnly From = new(2026, 10, 12);
    private static readonly UserId Child = new(Guid.Parse("01900000-0000-7000-8000-000000000001"));

    private static readonly Meal RatedServed = NewMeal("Rated, served", rated: true);
    private static readonly Meal RatedNotServed = NewMeal("Rated, not served", rated: true);
    private static readonly Meal UnratedServed = NewMeal("Unrated, served", rated: false);
    private static readonly Meal UnratedNotServed = NewMeal("Unrated, not served", rated: false);
    private static readonly Meal[] AllMeals = [RatedServed, RatedNotServed, UnratedServed, UnratedNotServed];

    private static readonly MealPlan Plan = NewPlan(
        (From.AddDays(-3), RatedServed),
        (From.AddDays(-10), UnratedServed));

    public static TheoryData<bool, AiServedWindow, string[]> FilterCombinations => new()
    {
        { false, AiServedWindow.Any, ["Rated, served", "Rated, not served", "Unrated, served", "Unrated, not served"] },
        { true, AiServedWindow.Any, ["Rated, served", "Rated, not served"] },
        { false, AiServedWindow.Last60Days, ["Rated, served", "Unrated, served"] },
        { true, AiServedWindow.Last60Days, ["Rated, served"] },
    };

    [Theory]
    [MemberData(nameof(FilterCombinations))]
    public void The_rated_and_served_filters_combine_with_and(bool ratedOnly, AiServedWindow servedWithin, string[] expected)
    {
        var selection = AiMealFilter.Apply(AllMeals, Plan, From, ratedOnly, servedWithin, []);

        Assert.Equal(expected, selection.Meals.Select(m => m.Name));
        Assert.False(selection.FilterMatchedNothing);
    }

    [Fact]
    public void The_window_covers_the_n_days_before_from_and_excludes_from_itself()
    {
        var onFirstDay = NewMeal("First day", rated: false);
        var dayBefore = NewMeal("Day before window", rated: false);
        var onFrom = NewMeal("On from", rated: false);
        var lastDay = NewMeal("Last day", rated: false);
        var plan = NewPlan(
            (From.AddDays(-30), onFirstDay),
            (From.AddDays(-31), dayBefore),
            (From, onFrom),
            (From.AddDays(-1), lastDay));

        var selection = AiMealFilter.Apply([onFirstDay, dayBefore, onFrom, lastDay], plan, From, false, AiServedWindow.Last30Days, []);

        Assert.Equal(["First day", "Last day"], selection.Meals.Select(m => m.Name));
    }

    [Fact]
    public void A_must_include_meal_is_offered_even_when_it_fails_the_filter()
    {
        var selection = AiMealFilter.Apply(AllMeals, Plan, From, true, AiServedWindow.Last30Days, [UnratedNotServed.Id]);

        Assert.Equal(["Rated, served", "Unrated, not served"], selection.Meals.Select(m => m.Name));
        Assert.False(selection.FilterMatchedNothing);
    }

    [Fact]
    public void Must_include_meals_do_not_count_as_a_filter_match()
    {
        var selection = AiMealFilter.Apply([UnratedNotServed], Plan, From, true, AiServedWindow.Any, [UnratedNotServed.Id]);

        Assert.True(selection.FilterMatchedNothing);
        Assert.Equal([UnratedNotServed.Id], selection.Meals.Select(m => m.Id));
    }

    [Fact]
    public void Archived_meals_are_never_offered()
    {
        var archived = RatedServed with { IsArchived = true };

        var unfiltered = AiMealFilter.Apply([archived, RatedNotServed], Plan, From, false, AiServedWindow.Any, [archived.Id]);
        var filtered = AiMealFilter.Apply([archived, RatedNotServed], Plan, From, false, AiServedWindow.Last30Days, [archived.Id]);

        Assert.Equal(["Rated, not served"], unfiltered.Meals.Select(m => m.Name));
        Assert.Empty(filtered.Meals);
        Assert.True(filtered.FilterMatchedNothing);
    }

    [Fact]
    public void A_family_without_a_plan_has_nothing_served()
    {
        var selection = AiMealFilter.Apply(AllMeals, null, From, false, AiServedWindow.Last90Days, []);

        Assert.Empty(selection.Meals);
        Assert.True(selection.FilterMatchedNothing);
    }

    [Fact]
    public void An_unfiltered_family_with_no_meals_is_not_a_filter_mismatch()
    {
        var selection = AiMealFilter.Apply([], null, From, false, AiServedWindow.Any, []);

        Assert.False(selection.FilterMatchedNothing);
    }

    [Fact]
    public void The_prompt_describes_an_active_filter()
    {
        var prompt = AiSessionPromptBuilder.Build(NewSession(), [RatedServed], NewStarted(true, AiServedWindow.Last60Days));

        Assert.Contains(
            "The guardian limited the meal list to meals the children have rated that were served between 2026-08-13 and 2026-10-11",
            prompt,
            StringComparison.Ordinal);
    }

    [Fact]
    public void The_prompt_describes_a_rated_only_filter_without_dates()
    {
        var prompt = AiSessionPromptBuilder.Build(NewSession(), [RatedServed], NewStarted(true, AiServedWindow.Any));

        Assert.Contains("The guardian limited the meal list to meals the children have rated.", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void The_prompt_mentions_must_include_meals_in_the_filter_line_only_when_there_are_some()
    {
        var started = NewStarted(false, AiServedWindow.Last30Days) with { MustIncludeMealIds = [RatedServed.Id] };

        var prompt = AiSessionPromptBuilder.Build(NewSession(), [RatedServed], started);

        Assert.Contains("2026-10-11, plus the meal ids they asked to include.", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void The_prompt_has_no_filter_line_without_a_filter()
    {
        var prompt = AiSessionPromptBuilder.Build(NewSession(), [RatedServed], NewStarted(false, AiServedWindow.Any));

        Assert.DoesNotContain("limited the meal list", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void A_session_started_before_the_filter_existed_reads_as_unfiltered()
    {
        const string olderShape = """
            {
              "Id": "00000000-0000-0000-0000-000000000071",
              "ChildId": "00000000-0000-0000-0000-000000000003",
              "From": "2025-06-02",
              "To": "2025-06-08",
              "RequestedSlots": ["Dinner"],
              "MustIncludeMealIds": [],
              "Notes": "",
              "StartedBy": "00000000-0000-0000-0000-000000000001",
              "OccurredAt": "2025-01-01T12:00:00+00:00"
            }
            """;

        var started = JsonSerializer.Deserialize<AiSessionStarted>(olderShape, EventShapeTestSupport.CreateEventSerializerOptions());

        Assert.NotNull(started);
        Assert.False(started.RatedOnly);
        Assert.Equal(AiServedWindow.Any, started.ServedWithin);
    }

    private static MealplanAiSession NewSession() => new(
        MealplanAiSessionId.New(),
        From,
        From.AddDays(6),
        [MealSlot.Dinner],
        ImmutableDictionary<(DateOnly, MealSlot), MealId>.Empty,
        AiSessionStatus.Drafting);

    private static AiSessionStarted NewStarted(bool ratedOnly, AiServedWindow servedWithin) => new(
        MealplanAiSessionId.New(), Child, From, From.AddDays(6), [MealSlot.Dinner], [], "", Child, DateTimeOffset.UtcNow,
        ratedOnly, servedWithin);

    private static Meal NewMeal(string name, bool rated)
    {
        var ratings = rated
            ? ImmutableDictionary<UserId, MealRating>.Empty.Add(Child, new MealRating(3, "", DateTimeOffset.UtcNow))
            : ImmutableDictionary<UserId, MealRating>.Empty;
        return new Meal(new MealId(Guid.CreateVersion7()), Child, name, "", new Icon("🍝"), new Color("#ff0000"), false, ratings, Child);
    }

    private static MealPlan NewPlan(params (DateOnly Date, Meal Meal)[] assignments) => new(
        new MealPlanId(Guid.CreateVersion7()),
        assignments.ToImmutableDictionary(a => (a.Date, MealSlot.Dinner), a => new MealPlanAssignment(a.Meal.Id, Child, "")),
        ImmutableDictionary<MealSlot, TimeOnly>.Empty,
        ImmutableDictionary<buddy.Features.Mealplans.IcalTokenId, buddy.Features.Mealplans.IcalTokenInfo>.Empty,
        SharedWithGroupId: null);
}
