using buddy.Common;
using buddy.Common.Ical;
using buddy.Common.RateLimiting;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Calendars;

public static class GetIcalFeedEndpoint
{
    public static RouteGroupBuilder MapGetIcalFeed(this RouteGroupBuilder calendars)
    {
        calendars.MapGet("/{calendarId:guid}/ical/{token}", async Task<Results<ContentHttpResult, NotFound>> (
            Guid calendarId,
            string token,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var query = new GetIcalFeed(new CalendarId(calendarId), token);
            var result = await bus.InvokeAsync<Result<string>>(query, cancellationToken);

            httpContext.Response.Headers.CacheControl = IcalSubscription.CacheControl;

            return result switch
            {
                Result<string>.Success(var icsContent) => TypedResults.Text(icsContent, IcalSubscription.ContentType),
                Result<string>.NotFound => TypedResults.NotFound(),
                // GetIcalFeedHandler has no access-check or validation concept -- these are
                // unreachable today, collapsed to NotFound since this route declares no other
                // status for them.
                Result<string>.Forbidden => TypedResults.NotFound(),
                Result<string>.Validation => TypedResults.NotFound(),
            };
        })
        .AllowAnonymous()
        .RequireRateLimiting(RateLimitingFeature.IcalFeedPolicy)
        .WithName("GetCalendarIcalFeed");

        return calendars;
    }
}
