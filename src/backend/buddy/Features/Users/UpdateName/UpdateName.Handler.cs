using buddy.Common;
using buddy.Common.Validation;

using FluentValidation;

namespace buddy.Features.Users;

public static class UpdateNameHandler
{
    public static async Task<Result<User>> Handle(UpdateName command, IValidator<UpdateName> validator, IUserEventStore events, TimeProvider time, CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<User>.Validation(problem);
        }

        var userId = command.UserId;

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

        var nameUpdated = new NameUpdated(userId, user.Name, command.Name, time.GetUtcNow());
        await events.AppendAsync(userId, [nameUpdated], cancellationToken);

        return new Result<User>.Success(user with { Name = command.Name });
    }
}
