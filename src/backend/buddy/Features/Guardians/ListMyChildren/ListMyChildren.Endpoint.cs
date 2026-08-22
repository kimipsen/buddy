using System.Security.Claims;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Guardians;

public static class ListMyChildrenEndpoint
{
    public static RouteGroupBuilder MapListMyChildren(this RouteGroupBuilder children)
    {
        children.MapGet("/", async Task<Ok<IReadOnlyCollection<ChildSummary>>> (
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var result = await bus.InvokeAsync<IReadOnlyCollection<ChildSummary>>(ListMyChildren.FromClaims(principal), cancellationToken);

            return TypedResults.Ok(result);
        })
        .WithName("ListMyChildren");

        return children;
    }
}
