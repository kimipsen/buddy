namespace buddy.Features.Users;

public static class UserHandler
{
    public static async Task<User> Handle(GetOrCreateUser command, IUserEventStore events, CancellationToken cancellationToken)
    {
        var userId = await events.FindUserIdAsync(command.Subject, cancellationToken);

        if (userId is not null)
        {
            var existingEvents = await events.ReadAsync(userId, cancellationToken);
            return Rehydrate(existingEvents)!;
        }

        var created = new UserCreated(
            UserId.New(),
            command.Subject,
            command.EmailVerified
                ? Email.Verified(command.Email ?? "")
                : Email.Unverified(command.Email ?? ""),
            command.UserName,
            command.Name,
            DateTimeOffset.UtcNow);

        var resultEvents = await events.CreateAsync(command.Subject, created.UserId, [created], cancellationToken);

        return Rehydrate(resultEvents)!;
    }

    public static async Task<IReadOnlyCollection<UserEvent>> Handle(GetUserEvents query, IUserEventStore events, CancellationToken cancellationToken)
    {
        var userId = await events.FindUserIdAsync(query.Subject, cancellationToken);

        return userId is null
            ? []
            : await events.ReadAsync(userId, cancellationToken);
    }

    public static async Task Handle(DeleteUser command, IUserEventStore events, CancellationToken cancellationToken)
    {
        var userId = await events.FindUserIdAsync(command.Subject, cancellationToken);

        if (userId is null)
        {
            return;
        }

        var existingEvents = await events.ReadAsync(userId, cancellationToken);
        var user = Rehydrate(existingEvents);

        if (user is null || user.IsDeleted)
        {
            return;
        }

        await events.AppendAsync(userId, [new UserDeleted(userId, DateTimeOffset.UtcNow)], cancellationToken);
    }

    private static User? Rehydrate(IEnumerable<UserEvent> events)
    {
        User? user = null;

        foreach (var @event in events)
        {
            user = @event switch
            {
                UserCreated created => new User(
                    created.UserId,
                    created.KeycloakSubject,
                    created.Email,
                    created.UserName,
                    created.Name),
                UserDeleted => user! with { IsDeleted = true },
                _ => user
            };
        }

        return user;
    }
}
