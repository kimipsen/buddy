using System.Security.Claims;

using buddy.Common;
using buddy.Features.Calendars;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.TransitionWarnings;

public static class ClearItemWarningOverrideEndpoint
{
    public static RouteGroupBuilder MapClearItemWarningOverride(this RouteGroupBuilder transitionWarnings)
    {
        transitionWarnings.MapDelete("/children/{childId:guid}/items/{itemId:guid}", async Task<Results<NoContent, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid childId,
            Guid itemId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = ClearItemWarningOverride.FromClaims(principal, new UserId(childId), new CalendarItemId(itemId));
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.Forbidden => TypedResults.Forbid(),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                // No body to validate.
                Result<Unit>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("ClearItemWarningOverride");

        return transitionWarnings;
    }
}
