using buddy.Common;

namespace buddy.Features.Users;

public static class UpdateNameHandler
{
    public static async Task<Result<User>> Handle(UpdateName command, IUserEventStore events, CancellationToken cancellationToken)
    {
        if (command.UserId is not { } userId)
        {
            return new Result<User>.NotFound();
        }

        var existingEvents = await events.ReadAsync(userId, cancellationToken);
        var user = User.Rehydrate(existingEvents);

        if (user is null || user.IsDeleted)
        {
            return new Result<User>.NotFound();
        }

        if (user.Name == command.Name)
        {
            return new Result<User>.Success(user);
        }

        var nameUpdated = new NameUpdated(userId, user.Name, command.Name, DateTimeOffset.UtcNow);
        await events.AppendAsync(userId, [nameUpdated], cancellationToken);

        return new Result<User>.Success(user with { Name = command.Name });
    }
}
