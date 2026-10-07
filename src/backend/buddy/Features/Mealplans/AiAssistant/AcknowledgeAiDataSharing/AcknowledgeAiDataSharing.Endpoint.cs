using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class AcknowledgeAiDataSharingEndpoint
{
    public static RouteGroupBuilder MapAcknowledgeAiDataSharing(this RouteGroupBuilder mealplans)
    {
        mealplans.MapPut("/children/{childId:guid}/ai/data-sharing-acknowledgement", async Task<Results<Ok<AiProviderSettings>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid childId,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = AcknowledgeAiDataSharing.FromClaims(principal, new UserId(childId));
            var result = await bus.InvokeAsync<Result<AiProviderSettings>>(command, cancellationToken);

            return result switch
            {
                Result<AiProviderSettings>.Success(var settings) => TypedResults.Ok(settings),
                Result<AiProviderSettings>.Forbidden => TypedResults.Forbid(),
                Result<AiProviderSettings>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<AiProviderSettings>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("AcknowledgeAiDataSharing");

        return mealplans;
    }
}
