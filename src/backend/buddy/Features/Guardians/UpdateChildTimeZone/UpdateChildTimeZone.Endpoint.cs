using System.Security.Claims;

using buddy.Common;
using buddy.Features.Calendars;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Guardians;

public static class UpdateChildTimeZoneEndpoint
{
    public static RouteGroupBuilder MapUpdateChildTimeZone(this RouteGroupBuilder children)
    {
        children.MapPatch("/{childId:guid}/timezone", async Task<Results<Ok<ChildSummary>, BadRequest<string>, NotFound>> (
            ClaimsPrincipal principal,
            Guid childId,
            UpdateChildTimeZoneRequest request,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.TimeZoneId))
            {
                return TypedResults.BadRequest($"The '{nameof(request.TimeZoneId)}' field is required.");
            }

            var command = UpdateChildTimeZone.FromClaims(principal, new UserId(childId), new TimeZoneId(request.TimeZoneId));
            var result = await bus.InvokeAsync<Result<ChildSummary>>(command, cancellationToken);

            return result switch
            {
                Result<ChildSummary>.Success(var summary) => TypedResults.Ok(summary),
                Result<ChildSummary>.NotFound => TypedResults.NotFound(),
                Result<ChildSummary>.Validation(var message) => TypedResults.BadRequest(message),
                // UpdateChildTimeZoneHandler never produces Forbidden -- collapsed to NotFound since
                // this route declares no other status for it.
                Result<ChildSummary>.Forbidden => TypedResults.NotFound(),
            };
        })
        .WithName("UpdateChildTimeZone");

        return children;
    }
}

public sealed record UpdateChildTimeZoneRequest(string TimeZoneId);
