using buddy.Email;

namespace buddy.Features.Users;

public static class UpdateEmailHandler
{
    public static async Task<User?> Handle(UpdateEmail command, IUserEventStore events, IEmailSender emailSender, CancellationToken cancellationToken)
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

        if (user.Email.Value == command.Value)
        {
            return user;
        }

        // A verification against the old address says nothing about the new one, so
        // changing the address always drops back to unverified, and a fresh verification
        // is requested for it right away.
        var now = DateTimeOffset.UtcNow;
        var newEmail = Email.Unverified(command.Value);
        var emailUpdated = new EmailUpdated(userId, user.Email, newEmail, now);

        var (token, hash, expiresAt) = EmailVerificationToken.Generate(now);
        var verificationRequested = new EmailVerificationRequested(userId, hash, expiresAt, now);

        await events.AppendAsync(userId, [emailUpdated, verificationRequested], cancellationToken);

        var updated = user with
        {
            Email = newEmail,
            EmailVerificationTokenHash = hash,
            EmailVerificationRequestedAt = now,
            EmailVerificationExpiresAt = expiresAt
        };

        await emailSender.SendEmailVerificationAsync(newEmail.Value, token, cancellationToken);

        return updated;
    }
}
