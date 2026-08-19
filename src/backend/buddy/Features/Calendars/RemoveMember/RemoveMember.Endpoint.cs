using System.Security.Claims;

using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Calendars;

public static class RemoveMemberEndpoint
{
    public static RouteGroupBuilder MapRemoveMember(this RouteGroupBuilder calendars)
    {
        calendars.MapDelete("/{calendarId:guid}/members/{memberId:guid}", async Task<Results<NoContent, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid calendarId,
            Guid memberId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = RemoveMember.FromClaims(principal, new CalendarId(calendarId), new UserId(memberId));
            var access = await bus.InvokeAsync<CalendarAccess>(command, cancellationToken);

            return access switch
            {
                CalendarAccess.Allowed => TypedResults.NoContent(),
                CalendarAccess.Forbidden => TypedResults.Forbid(),
                _ => TypedResults.NotFound(),
            };
        })
        .WithName("RemoveCalendarMember");

        return calendars;
    }
}
