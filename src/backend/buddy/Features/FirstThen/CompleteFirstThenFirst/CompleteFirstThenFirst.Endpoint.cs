using System.Security.Claims;

using buddy.Common.OpenApi;
using buddy.Features.Users;

using Wolverine;

namespace buddy.Features.FirstThen;

public static class CompleteFirstThenFirstEndpoint
{
    public static RouteGroupBuilder MapCompleteFirstThenFirst(this RouteGroupBuilder firstThen)
    {
        firstThen.MapPost("/children/{childId:guid}/first/complete", async (ClaimsPrincipal principal, Guid childId, FirstThenRevisionRequest request, IMessageBus bus, HttpContext httpContext, CancellationToken cancellationToken) =>
            (await bus.InvokeAsync<FirstThenWriteOutcome>(CompleteFirstThenFirst.FromClaims(principal, new UserId(childId), request), cancellationToken)).ToHttpResult(httpContext))
            .ProducesErrorCode(StatusCodes.Status409Conflict, FirstThenWriteOutcome.BoardChangedCode)
            .WithName("CompleteFirstThenFirst");

        firstThen.MapPost("/me/first/complete", async (ClaimsPrincipal principal, FirstThenRevisionRequest request, IMessageBus bus, HttpContext httpContext, CancellationToken cancellationToken) =>
            (await bus.InvokeAsync<FirstThenWriteOutcome>(CompleteFirstThenFirst.FromClaims(principal, null, request), cancellationToken)).ToHttpResult(httpContext))
            .ProducesErrorCode(StatusCodes.Status409Conflict, FirstThenWriteOutcome.BoardChangedCode)
            .WithName("CompleteMyFirstThenFirst");

        return firstThen;
    }
}

// The revision of the board the caller was shown: 0 before the first board.
public sealed record FirstThenRevisionRequest(int Revision);
