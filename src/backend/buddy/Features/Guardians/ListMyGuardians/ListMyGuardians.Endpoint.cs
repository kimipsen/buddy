using System.Security.Claims;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Guardians;

public static class ListMyGuardiansEndpoint
{
    public static RouteGroupBuilder MapListMyGuardians(this RouteGroupBuilder guardians)
    {
        guardians.MapGet("/", async Task<Ok<IReadOnlyCollection<GuardianSummary>>> (
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var result = await bus.InvokeAsync<IReadOnlyCollection<GuardianSummary>>(ListMyGuardians.FromClaims(principal), cancellationToken);

            return TypedResults.Ok(result);
        })
        .WithName("ListMyGuardians");

        return guardians;
    }
}
