namespace buddy.Features.Users;

public static class GetUserEventsHandler
{
    public static async Task<IReadOnlyCollection<UserEvent>> Handle(GetUserEvents query, IUserEventStore events, CancellationToken cancellationToken)
    {
        var userId = await events.FindUserIdAsync(query.Subject, cancellationToken);

        return userId is null
            ? []
            : await events.ReadAsync(userId, cancellationToken);
    }
}
