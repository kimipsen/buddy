using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class SetProviderApiKeyEndpoint
{
    public static RouteGroupBuilder MapSetProviderApiKey(this RouteGroupBuilder mealplans)
    {
        mealplans.MapPut("/children/{childId:guid}/ai/providers/{provider}/key", async Task<Results<Ok<AiProviderSettings>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid childId,
            AiProvider provider,
            SetProviderApiKeyRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = SetProviderApiKey.FromClaims(principal, new UserId(childId), provider, request.ApiKey);
            var result = await bus.InvokeAsync<Result<AiProviderSettings>>(command, cancellationToken);

            return result switch
            {
                Result<AiProviderSettings>.Success(var settings) => TypedResults.Ok(settings),
                Result<AiProviderSettings>.Forbidden => TypedResults.Forbid(),
                Result<AiProviderSettings>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<AiProviderSettings>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("SetProviderApiKey");

        return mealplans;
    }
}

public sealed record SetProviderApiKeyRequest(string ApiKey);
