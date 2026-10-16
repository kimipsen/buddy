using buddy.Common;
using buddy.Common.RateLimiting;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.VisitSummaries;

public static class GetSharedVisitSummaryEndpoint
{
    public static RouteGroupBuilder MapGetSharedVisitSummary(this RouteGroupBuilder visitSummaries)
    {
        visitSummaries.MapGet("/shared/{token}", async Task<Results<Ok<SharedVisitSummaryResponse>, NotFound>> (
            string token,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var result = await bus.InvokeAsync<Result<SharedVisitSummaryResponse>>(new GetSharedVisitSummary(token), cancellationToken);

            return result switch
            {
                Result<SharedVisitSummaryResponse>.Success(var shared) => TypedResults.Ok(shared),
                Result<SharedVisitSummaryResponse>.NotFound => TypedResults.NotFound(),
                // No access tiers and no validation on a token-gated read -- both unreachable.
                Result<SharedVisitSummaryResponse>.Forbidden => TypedResults.NotFound(),
                Result<SharedVisitSummaryResponse>.Validation => TypedResults.NotFound(),
            };
        })
        .AllowAnonymous()
        .RequireRateLimiting(RateLimitingFeature.VisitSummarySharePolicy)
        .WithName("GetSharedVisitSummary");

        return visitSummaries;
    }
}
