using buddy.Common;
using buddy.Common.Ical;
using buddy.Common.RateLimiting;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.NightDuty;

public static class GetNightIcalFeedEndpoint
{
    public static RouteGroupBuilder MapGetNightIcalFeed(this RouteGroupBuilder nightDuty)
    {
        nightDuty.MapGet("/feeds/{feedId:guid}/ical/{token}", async Task<Results<ContentHttpResult, NotFound>> (
            Guid feedId,
            string token,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var query = new GetNightIcalFeed(new NightFeedId(feedId), token);
            var result = await bus.InvokeAsync<Result<string>>(query, cancellationToken);

            httpContext.Response.Headers.CacheControl = IcalSubscription.CacheControl;

            return result switch
            {
                Result<string>.Success(var icsContent) => TypedResults.Text(icsContent, IcalSubscription.ContentType),
                Result<string>.NotFound => TypedResults.NotFound(),
                // GetNightIcalFeedHandler folds every denial into NotFound and has no validation --
                // these are unreachable, collapsed to NotFound since this route declares no other
                // status for them.
                Result<string>.Forbidden => TypedResults.NotFound(),
                Result<string>.Validation => TypedResults.NotFound(),
            };
        })
        .AllowAnonymous()
        .RequireRateLimiting(RateLimitingFeature.IcalFeedPolicy)
        // ContentHttpResult adds no response metadata.
        .Produces<string>(StatusCodes.Status200OK, "text/calendar")
        .WithName("GetNightIcalFeed");

        return nightDuty;
    }
}
