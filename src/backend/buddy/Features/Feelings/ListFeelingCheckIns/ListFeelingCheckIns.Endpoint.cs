using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Feelings;

public static class ListFeelingCheckInsEndpoint
{
    public static RouteGroupBuilder MapListFeelingCheckIns(this RouteGroupBuilder feelings)
    {
        feelings.MapGet("/children/{childId:guid}/check-ins", async Task<Results<Ok<FeelingCheckInsResponse>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid childId,
            DateOnly from,
            DateOnly to,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var query = ListFeelingCheckIns.FromClaims(principal, new UserId(childId), from, to);
            var result = await bus.InvokeAsync<Result<FeelingCheckInsResponse>>(query, cancellationToken);

            return result switch
            {
                Result<FeelingCheckInsResponse>.Success(var checkIns) => TypedResults.Ok(checkIns),
                Result<FeelingCheckInsResponse>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<FeelingCheckInsResponse>.NotFound => TypedResults.NotFound(),
                Result<FeelingCheckInsResponse>.Forbidden => TypedResults.Forbid(),
            };
        })
        .WithName("ListFeelingCheckIns");

        return feelings;
    }
}
