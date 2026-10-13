using System.Security.Claims;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.TransitionWarnings;

public static class GetMyTransitionWarningsEndpoint
{
    public static RouteGroupBuilder MapGetMyTransitionWarnings(this RouteGroupBuilder transitionWarnings)
    {
        transitionWarnings.MapGet("/me", async Task<Ok<MyTransitionWarningsResponse>> (
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var result = await bus.InvokeAsync<MyTransitionWarningsResponse>(GetMyTransitionWarnings.FromClaims(principal), cancellationToken);

            return TypedResults.Ok(result);
        })
        .WithName("GetMyTransitionWarnings");

        return transitionWarnings;
    }
}
