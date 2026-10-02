namespace buddy.Features.Users;

public static class GetUserEventsHandler
{
    public static async Task<UserEventsPage> Handle(GetUserEvents query, IUserEventStore events, CancellationToken cancellationToken)
    {
        var userId = query.UserId;

        var position = query.Page.Position;

        return position.Direction switch
        {
            CursorDirection.Before => await HandleBackward(userId, position.Version, query.Page.PageSize, events, cancellationToken),
            CursorDirection.After => await HandleForward(userId, position.Version, query.Page.PageSize, events, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(query), position.Direction, "Unknown cursor direction."),
        };
    }

    private static async Task<UserEventsPage> HandleForward(UserId userId, long afterVersion, int pageSize, IUserEventStore events, CancellationToken cancellationToken)
    {
        // Fetch one extra entry so its presence alone tells us whether another page follows,
        // without a separate count query.
        var entries = await events.ReadForwardAsync(userId, afterVersion, pageSize + 1, cancellationToken);
        var hasNext = entries.Count > pageSize;
        var page = entries.Take(pageSize).ToArray();

        // Stream versions are contiguous, so this boundary math reconstructs the same "show
        // everything up to and including afterVersion" page even when this page came back empty
        // (e.g. paging forward past the end of the stream).
        var previousCursor = afterVersion > 0 ? Cursor.EncodeBefore(afterVersion + 1) : null;
        var nextCursor = hasNext ? Cursor.EncodeAfter(page[^1].Version) : null;

        return new UserEventsPage([.. page.Select(e => e.Event)], previousCursor, nextCursor);
    }

    private static async Task<UserEventsPage> HandleBackward(UserId userId, long beforeVersion, int pageSize, IUserEventStore events, CancellationToken cancellationToken)
    {
        var entries = await events.ReadBackwardAsync(userId, beforeVersion, pageSize + 1, cancellationToken);
        var hasPrevious = entries.Count > pageSize;
        var page = entries.TakeLast(pageSize).ToArray();

        var previousCursor = hasPrevious ? Cursor.EncodeBefore(page[0].Version) : null;

        // beforeVersion was minted from a real event's version by an earlier forward page, so
        // there is always at least that much data ahead of this page -- versions are contiguous,
        // so beforeVersion - 1 reconstructs the same cursor even if this page came back empty.
        var nextCursor = Cursor.EncodeAfter(beforeVersion - 1);

        return new UserEventsPage([.. page.Select(e => e.Event)], previousCursor, nextCursor);
    }
}

public sealed record UserEventsPage(IReadOnlyCollection<UserEvent> Events, string? PreviousCursor, string? NextCursor);

public sealed record UserEventResponse(string Type, object Data);
