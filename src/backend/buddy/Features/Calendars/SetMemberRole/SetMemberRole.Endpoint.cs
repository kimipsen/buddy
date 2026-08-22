using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Calendars;

public static class SetMemberRoleEndpoint
{
    public static RouteGroupBuilder MapSetMemberRole(this RouteGroupBuilder calendars)
    {
        calendars.MapPut("/{calendarId:guid}/members/{memberId:guid}", async Task<Results<NoContent, NotFound, ForbidHttpResult, BadRequest<string>>> (
            ClaimsPrincipal principal,
            Guid calendarId,
            Guid memberId,
            SetMemberRoleRequest request,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            if (request.Role == CalendarRole.Owner)
            {
                return TypedResults.BadRequest("Ownership cannot be granted through this endpoint.");
            }

            var command = SetMemberRole.FromClaims(principal, new CalendarId(calendarId), new UserId(memberId), request.Role);
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.Forbidden => TypedResults.Forbid(),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                // SetMemberRoleHandler never produces Validation, but BadRequest is already part
                // of this route's declared results (used above), so map it there if it ever did.
                Result<Unit>.Validation(var message) => TypedResults.BadRequest(message),
            };
        })
        .WithName("SetCalendarMemberRole");

        return calendars;
    }
}

public sealed record SetMemberRoleRequest(CalendarRole Role);
