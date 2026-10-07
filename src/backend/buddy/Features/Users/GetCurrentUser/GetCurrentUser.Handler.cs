using buddy.Common;
using buddy.Email;
using buddy.Features.Calendars;

namespace buddy.Features.Users;

public static class GetOrCreateUserHandler
{
    public static async Task<Result<User>> Handle(GetOrCreateUser command, IUserEventStore events, IEmailSender emailSender, ILogger<GetOrCreateUser> logger, CancellationToken cancellationToken)
    {
        var identity = await events.FindIdentityAsync(command.Subject, cancellationToken);

        // Never provision a deleted subject again, even while its Keycloak account still exists.
        if (identity is { Deleted: true })
        {
            return new Result<User>.NotFound();
        }

        if (identity?.UserId is { } userId)
        {
            var existingUser = await events.FindSnapshotAsync(userId, cancellationToken)
                ?? throw new InvalidOperationException($"No snapshot for userId {userId}, although its index says the stream exists.");

            return existingUser.IsDeleted ? new Result<User>.NotFound() : new Result<User>.Success(existingUser);
        }

        var now = DateTimeOffset.UtcNow;
        var email = command.EmailVerified
            ? Email.Verified(command.Email ?? "")
            : Email.Unverified(command.Email ?? "");

        // A new adult starts on UTC (they pick their own zone later) and the browser's language.
        var created = new UserCreated(
            UserId.New(),
            command.Subject,
            email,
            command.UserName,
            command.Name,
            TimeZoneId.Utc,
            SupportedLanguages.ResolveFromAcceptLanguageHeader(command.AcceptLanguageHeader),
            now);

        List<UserEvent> initialEvents = [created];
        string? verificationToken = null;

        if (!email.IsVerified && !string.IsNullOrWhiteSpace(email.Value))
        {
            var (token, hash, expiresAt) = EmailVerificationToken.Generate(now);
            verificationToken = token;
            initialEvents.Add(new EmailVerificationRequested(created.UserId, hash, expiresAt, now));
        }

        var resultEvents = await events.CreateAsync(command.Subject, created.UserId, initialEvents, cancellationToken);
        var user = User.Replay(resultEvents);

        // A concurrent first sign-in may have won the race; then this call created nothing.
        if (user.Id == created.UserId)
        {
            logger.UserProvisioned(user.Id.Value, email.IsVerified);
        }

        if (verificationToken is not null)
        {
            await emailSender.SendEmailVerificationAsync(user.Email.Value, verificationToken, cancellationToken);
        }

        return new Result<User>.Success(user);
    }
}
