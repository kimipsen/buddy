using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class SetActiveProviderEndpoint
{
    public static RouteGroupBuilder MapSetActiveProvider(this RouteGroupBuilder mealplans)
    {
        mealplans.MapPut("/children/{childId:guid}/ai/active-provider/{provider}", async Task<Results<Ok<AiProviderSettings>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid childId,
            AiProvider provider,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = SetActiveProvider.FromClaims(principal, new UserId(childId), provider);
            var result = await bus.InvokeAsync<Result<AiProviderSettings>>(command, cancellationToken);

            return result switch
            {
                Result<AiProviderSettings>.Success(var settings) => TypedResults.Ok(settings),
                Result<AiProviderSettings>.Forbidden => TypedResults.Forbid(),
                Result<AiProviderSettings>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<AiProviderSettings>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("SetActiveProvider");

        return mealplans;
    }
}

public sealed record SetActiveProviderRequest(AiProvider Provider);
