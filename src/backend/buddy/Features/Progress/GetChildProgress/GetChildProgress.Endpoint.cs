using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Progress;

public static class GetChildProgressEndpoint
{
    public static RouteGroupBuilder MapGetChildProgress(this RouteGroupBuilder progress)
    {
        progress.MapGet("/children/{childId:guid}", async Task<Results<Ok<ProgressSummary>, NotFound>> (
            ClaimsPrincipal principal,
            Guid childId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var result = await bus.InvokeAsync<Result<ProgressSummary>>(GetChildProgress.FromClaims(principal, new UserId(childId)), cancellationToken);

            return result switch
            {
                Result<ProgressSummary>.Success(var summary) => TypedResults.Ok(summary),
                Result<ProgressSummary>.NotFound => TypedResults.NotFound(),
                // GetChildProgressHandler never produces Forbidden or Validation.
                Result<ProgressSummary>.Forbidden => TypedResults.NotFound(),
                Result<ProgressSummary>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("GetChildProgress");

        return progress;
    }
}
