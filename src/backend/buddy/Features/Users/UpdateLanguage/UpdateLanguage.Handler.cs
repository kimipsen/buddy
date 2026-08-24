using buddy.Common;

namespace buddy.Features.Users;

public static class UpdateLanguageHandler
{
    public static async Task<Result<User>> Handle(UpdateLanguage command, IUserEventStore events, CancellationToken cancellationToken)
    {
        if (command.UserId is not { } userId)
        {
            return new Result<User>.NotFound();
        }

        if (!SupportedLanguages.IsValid(command.Language))
        {
            return new Result<User>.Validation($"'{command.Language.Value}' is not a supported language.");
        }

        var existingEvents = await events.ReadAsync(userId, cancellationToken);
        var user = User.Rehydrate(existingEvents);

        if (user is null || user.IsDeleted)
        {
            return new Result<User>.NotFound();
        }

        if (user.ResolvedLanguage == command.Language)
        {
            return new Result<User>.Success(user);
        }

        var languageUpdated = new LanguageUpdated(userId, user.ResolvedLanguage, command.Language, DateTimeOffset.UtcNow);
        await events.AppendAsync(userId, [languageUpdated], cancellationToken);

        return new Result<User>.Success(user with { Language = command.Language });
    }
}
