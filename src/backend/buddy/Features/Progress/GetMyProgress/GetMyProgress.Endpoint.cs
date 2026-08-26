using System.Security.Claims;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Progress;

public static class GetMyProgressEndpoint
{
    public static RouteGroupBuilder MapGetMyProgress(this RouteGroupBuilder progress)
    {
        progress.MapGet("/me", async Task<Ok<ProgressSummary>> (
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var result = await bus.InvokeAsync<ProgressSummary>(GetMyProgress.FromClaims(principal), cancellationToken);

            return TypedResults.Ok(result);
        })
        .WithName("GetMyProgress");

        return progress;
    }
}
