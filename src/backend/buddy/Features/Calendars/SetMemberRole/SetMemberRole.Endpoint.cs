using System.Security.Claims;

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
            var access = await bus.InvokeAsync<CalendarAccess>(command, cancellationToken);

            return access switch
            {
                CalendarAccess.Allowed => TypedResults.NoContent(),
                CalendarAccess.Forbidden => TypedResults.Forbid(),
                _ => TypedResults.NotFound(),
            };
        })
        .WithName("SetCalendarMemberRole");

        return calendars;
    }
}

public sealed record SetMemberRoleRequest(CalendarRole Role);
