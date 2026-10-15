using System.Security.Claims;

using buddy.Common.OpenApi;
using buddy.Features.Calendars;
using buddy.Features.Users;

using Wolverine;

namespace buddy.Features.FirstThen;

public static class SetFirstThenBoardEndpoint
{
    public static RouteGroupBuilder MapSetFirstThenBoard(this RouteGroupBuilder firstThen)
    {
        firstThen.MapPut("/children/{childId:guid}", async (ClaimsPrincipal principal, Guid childId, SetFirstThenBoardRequest request, IMessageBus bus, HttpContext httpContext, CancellationToken cancellationToken) =>
            (await bus.InvokeAsync<FirstThenWriteOutcome>(SetFirstThenBoard.FromClaims(principal, new UserId(childId), request), cancellationToken)).ToHttpResult(httpContext))
            .ProducesErrorCode(StatusCodes.Status409Conflict, FirstThenWriteOutcome.BoardChangedCode)
            .WithName("SetFirstThenBoard");

        return firstThen;
    }
}

public sealed record FirstThenCardRequest(string Title, string Icon)
{
    public FirstThenCard ToCard() => new(Title.Trim(), new Icon(Icon.Trim()));
}

// thenMinutes null means no countdown on the Then.
public sealed record SetFirstThenBoardRequest(int Revision, FirstThenCardRequest First, FirstThenCardRequest Then, int? ThenMinutes = null);
