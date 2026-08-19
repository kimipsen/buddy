namespace buddy.Features.Users;

public static class GetOrCreateUserHandler
{
    public static async Task<User> Handle(GetOrCreateUser command, IUserEventStore events, CancellationToken cancellationToken)
    {
        var userId = await events.FindUserIdAsync(command.Subject, cancellationToken);

        if (userId is not null)
        {
            var existingEvents = await events.ReadAsync(userId, cancellationToken);
            return User.Rehydrate(existingEvents)!;
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

        return User.Rehydrate(resultEvents)!;
    }
}
