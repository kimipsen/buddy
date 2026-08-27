using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Calendars;

using FluentValidation;

namespace buddy.Features.Users;

public static class UpdateTimeZoneHandler
{
    public static async Task<Result<User>> Handle(
        UpdateTimeZone command,
        IValidator<UpdateTimeZone> validator,
        IUserEventStore events,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<User>.Validation(problem);
        }

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

        if (user.ResolvedTimeZoneId == command.TimeZoneId)
        {
            return new Result<User>.Success(user);
        }

        var timeZoneUpdated = new TimeZoneUpdated(userId, user.ResolvedTimeZoneId, command.TimeZoneId, DateTimeOffset.UtcNow);
        await events.AppendAsync(userId, [timeZoneUpdated], cancellationToken);

        return new Result<User>.Success(user with { TimeZoneId = command.TimeZoneId });
    }
}
