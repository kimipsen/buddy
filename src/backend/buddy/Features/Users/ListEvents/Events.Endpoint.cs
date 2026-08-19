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
            string? cursor,
            int? pageSize,
            CancellationToken cancellationToken) =>
        {
            if (!EventCursor.TryDecode(cursor, out var afterVersion))
            {
                return Results.BadRequest($"The '{nameof(cursor)}' query parameter is not a valid pagination cursor.");
            }

            var command = GetUserEvents.FromClaims(
                principal,
                afterVersion,
                Math.Clamp(pageSize ?? GetUserEvents.DefaultPageSize, 1, GetUserEvents.MaxPageSize));

            var page = await bus.InvokeAsync<UserEventsPage>(command, cancellationToken);

            return Results.Ok(new UserEventsPageResponse(
                [.. page.Events.Select(e => new UserEventResponse(e.EventType, e.Value!))],
                page.NextVersion is { } nextVersion ? EventCursor.Encode(nextVersion) : null));
        })
        .WithName("GetCurrentUserEvents");

        return users;
    }
}

public sealed record UserEventsPageResponse(IReadOnlyCollection<UserEventResponse> Items, string? NextCursor);

internal static class EventCursor
{
    public static string Encode(long afterVersion) => Convert.ToBase64String(BitConverter.GetBytes(afterVersion));

    public static bool TryDecode(string? cursor, out long afterVersion)
    {
        if (string.IsNullOrEmpty(cursor))
        {
            afterVersion = 0;
            return true;
        }

        Span<byte> bytes = stackalloc byte[sizeof(long)];

        if (!Convert.TryFromBase64String(cursor, bytes, out var bytesWritten) || bytesWritten != sizeof(long))
        {
            afterVersion = 0;
            return false;
        }

        afterVersion = BitConverter.ToInt64(bytes);
        return afterVersion >= 0;
    }
}
