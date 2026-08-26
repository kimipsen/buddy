using buddy.Common;

namespace buddy.Features.Mealplans;

public static class GetMealPlanIcalFeedHandler
{
    // A rolling window, not "all entries ever" -- narrower than Calendars' GetIcalFeedHandler
    // (90/365 days) since a meal plan is realistically filled in much closer to the day itself than
    // a general calendar is (see docs/backend/analysis/mealplan-ical-feed.md).
    private static readonly TimeSpan LookBehind = TimeSpan.FromDays(14);
    private static readonly TimeSpan LookAhead = TimeSpan.FromDays(60);

    public static async Task<Result<string>> Handle(
        GetMealPlanIcalFeed query,
        IMealPlanEventStore mealPlans,
        IMealEventStore meals,
        CancellationToken cancellationToken)
    {
        var planEvents = await mealPlans.ReadAsync(query.MealPlanId, cancellationToken);
        var plan = MealPlan.Rehydrate(planEvents);

        // Same outcome (no feed) whether the plan doesn't exist or the token is wrong/revoked -- an
        // anonymous request can't distinguish which, by design.
        if (plan is null)
        {
            return new Result<string>.NotFound();
        }

        if (plan.FindMatchingToken(IcalToken.Hash(query.Token)) is null)
        {
            return new Result<string>.NotFound();
        }

        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        var from = today.AddDays(-LookBehind.Days);
        var to = today.AddDays(LookAhead.Days);

        var entries = await MealPlanExpansion.ExpandFromPlanAsync(plan, from, to, meals, null, cancellationToken);

        return new Result<string>.Success(MealPlanIcalFeedWriter.Write(plan, entries));
    }
}
