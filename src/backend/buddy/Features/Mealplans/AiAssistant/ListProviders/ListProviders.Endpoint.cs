using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class ListProvidersEndpoint
{
    public static RouteGroupBuilder MapListProviders(this RouteGroupBuilder mealplans)
    {
        mealplans.MapGet("/children/{childId:guid}/ai/providers", async Task<Results<Ok<AiProviderSettings>, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid childId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var query = ListProviders.FromClaims(principal, new UserId(childId));
            var result = await bus.InvokeAsync<Result<AiProviderSettings>>(query, cancellationToken);

            return result switch
            {
                Result<AiProviderSettings>.Success(var settings) => TypedResults.Ok(settings),
                Result<AiProviderSettings>.Forbidden => TypedResults.Forbid(),
                Result<AiProviderSettings>.NotFound => TypedResults.NotFound(),
                // ListProvidersHandler never validates -- there's no BadRequest in this route's
                // declared results, so this collapses to NotFound like the others.
                Result<AiProviderSettings>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("ListProviders");

        return mealplans;
    }
}
