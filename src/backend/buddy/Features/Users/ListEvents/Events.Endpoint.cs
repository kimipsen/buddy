using System.Security.Claims;
using Wolverine;

namespace buddy.Features.Users;

public static class ListUserEventsEndpoint
{
    public static RouteGroupBuilder MapListCurrentUserEvents(this RouteGroupBuilder users)
    {
        users.MapGet("/me/events", async (
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var userEvents = await bus.InvokeAsync<IReadOnlyCollection<UserEvent>>(GetUserEvents.FromClaims(principal), cancellationToken);

            return Results.Ok(userEvents.Select(e => new UserEventResponse(e.EventType, e.Value!)));
        })
        .WithName("GetCurrentUserEvents");

        return users;
    }
}
