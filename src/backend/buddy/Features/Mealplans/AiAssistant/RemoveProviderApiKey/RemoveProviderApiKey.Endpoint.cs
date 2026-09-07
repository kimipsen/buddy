using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class RemoveProviderApiKeyEndpoint
{
    public static RouteGroupBuilder MapRemoveProviderApiKey(this RouteGroupBuilder mealplans)
    {
        mealplans.MapDelete("/children/{childId:guid}/ai/providers/{provider}/key", async Task<Results<Ok<AiProviderSettings>, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid childId,
            AiProvider provider,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = RemoveProviderApiKey.FromClaims(principal, new UserId(childId), provider);
            var result = await bus.InvokeAsync<Result<AiProviderSettings>>(command, cancellationToken);

            return result switch
            {
                Result<AiProviderSettings>.Success(var settings) => TypedResults.Ok(settings),
                Result<AiProviderSettings>.Forbidden => TypedResults.Forbid(),
                Result<AiProviderSettings>.NotFound => TypedResults.NotFound(),
                // RemoveProviderApiKeyHandler never validates -- there's no BadRequest in this
                // route's declared results, so this collapses to NotFound like the others.
                Result<AiProviderSettings>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("RemoveProviderApiKey");

        return mealplans;
    }
}
