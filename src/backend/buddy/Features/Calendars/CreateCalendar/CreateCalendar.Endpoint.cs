using System.Security.Claims;

using buddy.Features.Groups;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Calendars;

public static class CreateCalendarEndpoint
{
    public static RouteGroupBuilder MapCreateCalendar(this RouteGroupBuilder calendars)
    {
        calendars.MapPost("/", async Task<Results<Ok<CalendarResponse>, UnauthorizedHttpResult, ForbidHttpResult, BadRequest<string>>> (
            ClaimsPrincipal principal,
            CreateCalendarRequest request,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var groupId = request.GroupId is { } value ? new GroupId(value) : (GroupId?)null;
            var command = CreateCalendar.FromClaims(principal, request.Name, new TimeZoneId(request.TimeZoneId), groupId);
            var result = await bus.InvokeAsync<CreateCalendarOutcome>(command, cancellationToken);

            return result switch
            {
                CreateCalendarOutcome.Success(var calendar) => TypedResults.Ok(CalendarResponse.FromCalendar(calendar)),
                CreateCalendarOutcome.Unauthenticated => TypedResults.Unauthorized(),
                CreateCalendarOutcome.Forbidden => TypedResults.Forbid(),
                CreateCalendarOutcome.Validation(var message) => TypedResults.BadRequest(message),
            };
        })
        .WithName("CreateCalendar");

        return calendars;
    }
}

public sealed record CreateCalendarRequest(string Name, string TimeZoneId, Guid? GroupId = null);
