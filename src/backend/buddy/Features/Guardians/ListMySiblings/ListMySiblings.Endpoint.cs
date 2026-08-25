using System.Security.Claims;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Guardians;

public static class ListMySiblingsEndpoint
{
    public static RouteGroupBuilder MapListMySiblings(this RouteGroupBuilder siblings)
    {
        siblings.MapGet("/", async Task<Ok<IReadOnlyCollection<SiblingSummary>>> (
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var result = await bus.InvokeAsync<IReadOnlyCollection<SiblingSummary>>(ListMySiblings.FromClaims(principal), cancellationToken);

            return TypedResults.Ok(result);
        })
        .WithName("ListMySiblings");

        return siblings;
    }
}
