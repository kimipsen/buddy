using System.Security.Claims;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Users;

public static class ListUserEventsEndpoint
{
    public static RouteGroupBuilder MapListCurrentUserEvents(this RouteGroupBuilder users)
    {
        users.MapGet("/me/events", async Task<Results<Ok<UserEventsPageResponse>, BadRequest<string>>> (
            ClaimsPrincipal principal,
            IMessageBus bus,
            string? cursor,
            int? pageSize,
            CancellationToken cancellationToken) =>
        {
            if (!Cursor.TryDecode(cursor, out var decoded))
            {
                return TypedResults.BadRequest($"The '{nameof(cursor)}' query parameter is not a valid pagination cursor.");
            }

            var command = GetUserEvents.FromClaims(
                principal,
                new EventsPageRequest(
                    AfterVersion: decoded.Direction == CursorDirection.After ? decoded.Version : null,
                    BeforeVersion: decoded.Direction == CursorDirection.Before ? decoded.Version : null,
                    PageSize: Math.Clamp(pageSize ?? EventsPageRequest.DefaultPageSize, 1, EventsPageRequest.MaxPageSize)));

            var page = await bus.InvokeAsync<UserEventsPage>(command, cancellationToken);

            return TypedResults.Ok(new UserEventsPageResponse(
                [.. page.Events.Select(e => new UserEventResponse(e.EventType, e.Value!))],
                page.PreviousCursor,
                page.NextCursor));
        })
        .WithName("GetCurrentUserEvents");

        return users;
    }
}

public sealed record UserEventsPageResponse(IReadOnlyCollection<UserEventResponse> Items, string? PreviousCursor, string? NextCursor);
