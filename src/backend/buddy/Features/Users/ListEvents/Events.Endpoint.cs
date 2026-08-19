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
            if (!Cursor.TryDecode(cursor, out var afterVersion))
            {
                return TypedResults.BadRequest($"The '{nameof(cursor)}' query parameter is not a valid pagination cursor.");
            }

            var command = GetUserEvents.FromClaims(
                principal,
                afterVersion,
                Math.Clamp(pageSize ?? GetUserEvents.DefaultPageSize, 1, GetUserEvents.MaxPageSize));

            var page = await bus.InvokeAsync<UserEventsPage>(command, cancellationToken);

            return TypedResults.Ok(new UserEventsPageResponse(
                [.. page.Events.Select(e => new UserEventResponse(e.EventType, e.Value!))],
                page.NextVersion is { } nextVersion ? Cursor.Encode(nextVersion) : null));
        })
        .WithName("GetCurrentUserEvents");

        return users;
    }
}

public sealed record UserEventsPageResponse(IReadOnlyCollection<UserEventResponse> Items, string? NextCursor);
