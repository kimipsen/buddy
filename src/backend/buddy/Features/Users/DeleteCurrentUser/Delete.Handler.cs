namespace buddy.Features.Users;

public static class DeleteUserHandler
{
    public static async Task Handle(DeleteUser command, IUserEventStore events, CancellationToken cancellationToken)
    {
        var userId = await events.FindUserIdAsync(command.Subject, cancellationToken);

        if (userId is null)
        {
            return;
        }

        var existingEvents = await events.ReadAsync(userId, cancellationToken);
        var user = User.Rehydrate(existingEvents);

        if (user is null || user.IsDeleted)
        {
            return;
        }

        await events.AppendAsync(userId, [new UserDeleted(userId, DateTimeOffset.UtcNow)], cancellationToken);
    }
}
