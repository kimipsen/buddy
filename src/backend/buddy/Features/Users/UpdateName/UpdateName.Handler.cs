namespace buddy.Features.Users;

public static class UpdateNameHandler
{
    public static async Task<User?> Handle(UpdateName command, IUserEventStore events, CancellationToken cancellationToken)
    {
        if (command.UserId is not { } userId)
        {
            return null;
        }

        var existingEvents = await events.ReadAsync(userId, cancellationToken);
        var user = User.Rehydrate(existingEvents);

        if (user is null || user.IsDeleted)
        {
            return null;
        }

        if (user.Name == command.Name)
        {
            return user;
        }

        var nameUpdated = new NameUpdated(userId, user.Name, command.Name, DateTimeOffset.UtcNow);
        await events.AppendAsync(userId, [nameUpdated], cancellationToken);

        return user with { Name = command.Name };
    }
}
