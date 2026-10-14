using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.FirstThen;

public static class GetMyFirstThenBoardEndpoint
{
    public static RouteGroupBuilder MapGetMyFirstThenBoard(this RouteGroupBuilder firstThen)
    {
        firstThen.MapGet("/me", async Task<Results<Ok<FirstThenBoardResponse>, NotFound>> (
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var result = await bus.InvokeAsync<Result<FirstThenBoardResponse>>(GetMyFirstThenBoard.FromClaims(principal), cancellationToken);

            return result switch
            {
                Result<FirstThenBoardResponse>.Success(var board) => TypedResults.Ok(board),
                Result<FirstThenBoardResponse>.NotFound => TypedResults.NotFound(),
                // CheckOwn has no Forbidden outcome, and there are no validation rules.
                Result<FirstThenBoardResponse>.Forbidden => TypedResults.NotFound(),
                Result<FirstThenBoardResponse>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("GetMyFirstThenBoard");

        return firstThen;
    }
}
