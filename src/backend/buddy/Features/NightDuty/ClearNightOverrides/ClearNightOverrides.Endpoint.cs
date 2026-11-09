using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.NightDuty;

public static class ClearNightOverridesEndpoint
{
    public static RouteGroupBuilder MapClearNightOverrides(this RouteGroupBuilder nightDuty)
    {
        nightDuty.MapDelete("/children/{childId:guid}/overrides", async Task<Results<NoContent, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid childId,
            DateOnly from,
            DateOnly to,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = ClearNightOverrides.FromClaims(principal, new UserId(childId), from, to);
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.Forbidden => TypedResults.Forbid(),
                Result<Unit>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<Unit>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("ClearNightOverrides");

        return nightDuty;
    }
}
