using buddy.Email;

namespace buddy.Features.Users;

public static class GetOrCreateUserHandler
{
    public static async Task<User> Handle(GetOrCreateUser command, IUserEventStore events, IEmailSender emailSender, CancellationToken cancellationToken)
    {
        var userId = await events.FindUserIdAsync(command.Subject, cancellationToken);

        if (userId is not null)
        {
            var existingEvents = await events.ReadAsync(userId, cancellationToken);
            return User.Rehydrate(existingEvents)!;
        }

        var now = DateTimeOffset.UtcNow;
        var email = command.EmailVerified
            ? Email.Verified(command.Email ?? "")
            : Email.Unverified(command.Email ?? "");

        var created = new UserCreated(UserId.New(), command.Subject, email, command.UserName, command.Name, now);

        List<UserEvent> initialEvents = [created];
        string? verificationToken = null;

        if (!email.IsVerified && !string.IsNullOrWhiteSpace(email.Value))
        {
            var (token, hash, expiresAt) = EmailVerificationToken.Generate(now);
            verificationToken = token;
            initialEvents.Add(new EmailVerificationRequested(created.UserId, hash, expiresAt, now));
        }

        var resultEvents = await events.CreateAsync(command.Subject, created.UserId, initialEvents, cancellationToken);
        var user = User.Rehydrate(resultEvents)!;

        if (verificationToken is not null)
        {
            await emailSender.SendEmailVerificationAsync(user.Email.Value, verificationToken, cancellationToken);
        }

        return user;
    }
}
