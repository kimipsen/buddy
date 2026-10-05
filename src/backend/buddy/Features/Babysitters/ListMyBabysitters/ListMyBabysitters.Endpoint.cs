using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Babysitters;

public static class ListMyBabysittersEndpoint
{
    public static RouteGroupBuilder MapListMyBabysitters(this RouteGroupBuilder babysitters)
    {
        babysitters.MapGet("/me", async Task<Results<Ok<IReadOnlyCollection<BabysitterSummary>>, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var query = ListMyBabysitters.FromClaims(principal);
            var result = await bus.InvokeAsync<Result<IReadOnlyCollection<BabysitterSummary>>>(query, cancellationToken);

            return result switch
            {
                Result<IReadOnlyCollection<BabysitterSummary>>.Success(var babysitters) => TypedResults.Ok(babysitters),
                Result<IReadOnlyCollection<BabysitterSummary>>.Forbidden => TypedResults.Forbid(),
                // CheckManage never returns NotFound and there's no input to validate -- neither is in
                // this route's declared results, so both collapse to Forbid.
                Result<IReadOnlyCollection<BabysitterSummary>>.NotFound => TypedResults.Forbid(),
                Result<IReadOnlyCollection<BabysitterSummary>>.Validation => TypedResults.Forbid(),
            };
        })
        .WithName("ListMyBabysitters");

        return babysitters;
    }
}
