using buddy.Common;
using buddy.Common.Validation;

using FluentValidation;

namespace buddy.Features.Users;

public static class UpdateTimerPreferencesHandler
{
    public static async Task<Result<User>> Handle(
        UpdateTimerPreferences command,
        IValidator<UpdateTimerPreferences> validator,
        IUserEventStore events,
        TimeProvider time,
        CancellationToken cancellationToken)
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

        if (user.TimerPreferences == command.TimerPreferences)
        {
            return new Result<User>.Success(user);
        }

        var updated = new TimerPreferencesUpdated(userId, user.TimerPreferences, command.TimerPreferences, time.GetUtcNow());
        await events.AppendAsync(userId, [updated], cancellationToken);

        return new Result<User>.Success(user with { TimerPreferences = command.TimerPreferences });
    }
}
