using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.FirstThen;

public static class GetFirstThenBoardEndpoint
{
    public static RouteGroupBuilder MapGetFirstThenBoard(this RouteGroupBuilder firstThen)
    {
        firstThen.MapGet("/children/{childId:guid}", async Task<Results<Ok<GuardianFirstThenResponse>, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid childId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var result = await bus.InvokeAsync<Result<GuardianFirstThenResponse>>(GetFirstThenBoard.FromClaims(principal, new UserId(childId)), cancellationToken);

            return result switch
            {
                Result<GuardianFirstThenResponse>.Success(var board) => TypedResults.Ok(board),
                Result<GuardianFirstThenResponse>.NotFound => TypedResults.NotFound(),
                Result<GuardianFirstThenResponse>.Forbidden => TypedResults.Forbid(),
                // No validation rules on a read.
                Result<GuardianFirstThenResponse>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("GetFirstThenBoard");

        return firstThen;
    }
}
