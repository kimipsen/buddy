using System.Security.Claims;

using buddy.Common;
using buddy.Features.Groups;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Calendars;

public static class TransferCalendarToGroupEndpoint
{
    public static RouteGroupBuilder MapTransferCalendarToGroup(this RouteGroupBuilder calendars)
    {
        calendars.MapPut("/{calendarId:guid}/group/{groupId:guid}", async Task<Results<NoContent, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid calendarId,
            Guid groupId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = TransferCalendarToGroup.FromClaims(principal, new CalendarId(calendarId), new GroupId(groupId));
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.Forbidden => TypedResults.Forbid(),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                // TransferCalendarToGroupHandler never produces Validation -- there's no
                // BadRequest in this route's declared results, so this collapses to NotFound.
                Result<Unit>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("TransferCalendarToGroup");

        return calendars;
    }
}
