using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.TransitionWarnings;

public static class GetChildTransitionWarningsEndpoint
{
    public static RouteGroupBuilder MapGetChildTransitionWarnings(this RouteGroupBuilder transitionWarnings)
    {
        transitionWarnings.MapGet("/children/{childId:guid}", async Task<Results<Ok<ChildTransitionWarningsResponse>, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid childId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var query = GetChildTransitionWarnings.FromClaims(principal, new UserId(childId));
            var result = await bus.InvokeAsync<Result<ChildTransitionWarningsResponse>>(query, cancellationToken);

            return result switch
            {
                Result<ChildTransitionWarningsResponse>.Success(var response) => TypedResults.Ok(response),
                Result<ChildTransitionWarningsResponse>.Forbidden => TypedResults.Forbid(),
                Result<ChildTransitionWarningsResponse>.NotFound => TypedResults.NotFound(),
                // The handler takes no input to validate.
                Result<ChildTransitionWarningsResponse>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("GetChildTransitionWarnings");

        return transitionWarnings;
    }
}
