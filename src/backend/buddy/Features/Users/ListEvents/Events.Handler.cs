namespace buddy.Features.Users;

public static class GetUserEventsHandler
{
    public static async Task<UserEventsPage> Handle(GetUserEvents query, IUserEventStore events, CancellationToken cancellationToken)
    {
        var userId = await events.FindUserIdAsync(query.Subject, cancellationToken);

        if (userId is null)
        {
            return new UserEventsPage([], null);
        }

        // Fetch one extra entry so its presence alone tells us whether another page follows,
        // without a separate count query.
        var entries = await events.ReadPageAsync(userId, query.AfterVersion, query.PageSize + 1, cancellationToken);
        var hasMore = entries.Count > query.PageSize;
        var page = entries.Take(query.PageSize).ToArray();

        return new UserEventsPage(
            [.. page.Select(e => e.Event)],
            hasMore ? page[^1].Version : null);
    }
}

public sealed record UserEventsPage(IReadOnlyCollection<UserEvent> Events, long? NextVersion);

public sealed record UserEventResponse(string Type, object Data);
