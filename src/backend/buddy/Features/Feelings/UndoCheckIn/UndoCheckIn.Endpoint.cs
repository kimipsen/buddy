using System.Security.Claims;

using buddy.Common;
using buddy.Common.OpenApi;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Feelings;

public static class UndoCheckInEndpoint
{
    public static RouteGroupBuilder MapUndoCheckIn(this RouteGroupBuilder feelings)
    {
        feelings.MapDelete("/me/check-ins/{checkInId:guid}", async Task<Results<NoContent, NotFound, Conflict<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid checkInId,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = UndoCheckIn.FromClaims(principal, new FeelingCheckInId(checkInId));
            var outcome = await bus.InvokeAsync<UndoCheckInOutcome>(command, cancellationToken);

            return outcome.ToHttpResult(httpContext);
        })
        .ProducesErrorCode(StatusCodes.Status409Conflict, UndoCheckInOutcome.UndoWindowClosedCode)
        .WithName("UndoCheckIn");

        return feelings;
    }
}
