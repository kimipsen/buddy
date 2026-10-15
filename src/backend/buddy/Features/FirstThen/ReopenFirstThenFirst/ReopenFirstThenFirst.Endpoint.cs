using System.Security.Claims;

using buddy.Common.OpenApi;
using buddy.Features.Users;

using Wolverine;

namespace buddy.Features.FirstThen;

public static class ReopenFirstThenFirstEndpoint
{
    public static RouteGroupBuilder MapReopenFirstThenFirst(this RouteGroupBuilder firstThen)
    {
        firstThen.MapPost("/children/{childId:guid}/first/reopen", async (ClaimsPrincipal principal, Guid childId, FirstThenRevisionRequest request, IMessageBus bus, HttpContext httpContext, CancellationToken cancellationToken) =>
            (await bus.InvokeAsync<FirstThenWriteOutcome>(ReopenFirstThenFirst.FromClaims(principal, new UserId(childId), request), cancellationToken)).ToHttpResult(httpContext))
            .ProducesErrorCode(StatusCodes.Status409Conflict, FirstThenWriteOutcome.BoardChangedCode)
            .WithName("ReopenFirstThenFirst");

        firstThen.MapPost("/me/first/reopen", async (ClaimsPrincipal principal, FirstThenRevisionRequest request, IMessageBus bus, HttpContext httpContext, CancellationToken cancellationToken) =>
            (await bus.InvokeAsync<FirstThenWriteOutcome>(ReopenFirstThenFirst.FromClaims(principal, null, request), cancellationToken)).ToHttpResult(httpContext))
            .ProducesErrorCode(StatusCodes.Status409Conflict, FirstThenWriteOutcome.BoardChangedCode)
            .WithName("ReopenMyFirstThenFirst");

        return firstThen;
    }
}
