using buddy.Common;
using buddy.Common.Ical;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class GetMealPlanIcalFeedEndpoint
{
    public static RouteGroupBuilder MapGetMealPlanIcalFeed(this RouteGroupBuilder mealplans)
    {
        mealplans.MapGet("/{mealPlanId:guid}/ical/{token}", async Task<Results<ContentHttpResult, NotFound>> (
            Guid mealPlanId,
            string token,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var query = new GetMealPlanIcalFeed(new MealPlanId(mealPlanId), token);
            var result = await bus.InvokeAsync<Result<string>>(query, cancellationToken);

            httpContext.Response.Headers.CacheControl = IcalSubscription.CacheControl;

            return result switch
            {
                Result<string>.Success(var icsContent) => TypedResults.Text(icsContent, IcalSubscription.ContentType),
                Result<string>.NotFound => TypedResults.NotFound(),
                // GetMealPlanIcalFeedHandler has no access-check or validation concept -- these are
                // unreachable today, collapsed to NotFound since this route declares no other
                // status for them.
                Result<string>.Forbidden => TypedResults.NotFound(),
                Result<string>.Validation => TypedResults.NotFound(),
            };
        })
        .AllowAnonymous()
        .WithName("GetMealPlanIcalFeed");

        return mealplans;
    }
}
