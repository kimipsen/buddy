using System.Security.Claims;

using buddy.Common.OpenApi;
using buddy.Features.Users;

using Wolverine;

namespace buddy.Features.FirstThen;

public static class EndFirstThenBoardEndpoint
{
    public static RouteGroupBuilder MapEndFirstThenBoard(this RouteGroupBuilder firstThen)
    {
        firstThen.MapPost("/children/{childId:guid}/end", async (ClaimsPrincipal principal, Guid childId, FirstThenRevisionRequest request, IMessageBus bus, HttpContext httpContext, CancellationToken cancellationToken) =>
            (await bus.InvokeAsync<FirstThenWriteOutcome>(EndFirstThenBoard.FromClaims(principal, new UserId(childId), request), cancellationToken)).ToHttpResult(httpContext))
            .ProducesErrorCode(StatusCodes.Status409Conflict, FirstThenWriteOutcome.BoardChangedCode)
            .WithName("EndFirstThenBoard");

        firstThen.MapPost("/me/end", async (ClaimsPrincipal principal, FirstThenRevisionRequest request, IMessageBus bus, HttpContext httpContext, CancellationToken cancellationToken) =>
            (await bus.InvokeAsync<FirstThenWriteOutcome>(EndFirstThenBoard.FromClaims(principal, null, request), cancellationToken)).ToHttpResult(httpContext))
            .ProducesErrorCode(StatusCodes.Status409Conflict, FirstThenWriteOutcome.BoardChangedCode)
            .WithName("EndMyFirstThenBoard");

        return firstThen;
    }
}
