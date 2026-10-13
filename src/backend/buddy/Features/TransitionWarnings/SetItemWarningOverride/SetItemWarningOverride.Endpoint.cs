using System.Security.Claims;

using buddy.Common;
using buddy.Common.OpenApi;
using buddy.Features.Calendars;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.TransitionWarnings;

public static class SetItemWarningOverrideEndpoint
{
    public static RouteGroupBuilder MapSetItemWarningOverride(this RouteGroupBuilder transitionWarnings)
    {
        transitionWarnings.MapPut("/children/{childId:guid}/items/{itemId:guid}", async Task<Results<Ok<ChildTransitionWarningsResponse>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>, Conflict<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid childId,
            Guid itemId,
            SetItemWarningOverrideRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = SetItemWarningOverride.FromClaims(principal, new UserId(childId), new CalendarItemId(itemId), request.LeadMinutes);
            var outcome = await bus.InvokeAsync<SetItemWarningOverrideOutcome>(command, cancellationToken);

            return outcome.ToHttpResult(httpContext);
        })
        .ProducesErrorCode(StatusCodes.Status409Conflict, SetItemWarningOverrideOutcome.TooManyOverridesCode)
        .WithName("SetItemWarningOverride");

        return transitionWarnings;
    }
}

// The item's own lead times, replacing the child's defaults for it; an empty list means "never
// warn for this item".
public sealed record SetItemWarningOverrideRequest(IReadOnlyList<int> LeadMinutes);
