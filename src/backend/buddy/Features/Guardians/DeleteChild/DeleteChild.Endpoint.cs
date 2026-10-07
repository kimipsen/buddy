using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Guardians;

public static class DeleteChildEndpoint
{
    public static RouteGroupBuilder MapDeleteChild(this RouteGroupBuilder children)
    {
        children.MapDelete("/{childId:guid}", async Task<Results<NoContent, NotFound, Conflict<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid childId,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var outcome = await bus.InvokeAsync<DeleteChildOutcome>(DeleteChild.FromClaims(principal, new UserId(childId)), cancellationToken);

            return outcome switch
            {
                DeleteChildOutcome.Success => TypedResults.NoContent(),
                DeleteChildOutcome.NotFound => TypedResults.NotFound(),
                DeleteChildOutcome.HasOtherGuardians => TypedResults.Conflict(new ErrorEnvelope(
                    DeleteChild.HasOtherGuardiansCode,
                    "The child has other guardians. Each of them has to remove the child before it can be deleted.",
                    new Dictionary<string, string[]>(),
                    httpContext.TraceIdentifier)),
            };
        })
        .WithName("DeleteChild");

        return children;
    }
}
