using buddy.Email;

namespace buddy.Features.Users;

public static class ResendEmailVerificationHandler
{
    public static readonly TimeSpan ResendCooldown = TimeSpan.FromMinutes(1);

    public static async Task<ResendEmailVerificationResult> Handle(ResendEmailVerification command, IUserEventStore events, IEmailSender emailSender, CancellationToken cancellationToken)
    {
        if (command.UserId is not { } userId)
        {
            return ResendEmailVerificationResult.UserNotFound;
        }

        var existingEvents = await events.ReadAsync(userId, cancellationToken);
        var user = User.Rehydrate(existingEvents);

        if (user is null || user.IsDeleted)
        {
            return ResendEmailVerificationResult.UserNotFound;
        }

        if (user.Email.IsVerified)
        {
            return ResendEmailVerificationResult.AlreadyVerified;
        }

        var now = DateTimeOffset.UtcNow;

        if (user.EmailVerificationRequestedAt is { } requestedAt && now - requestedAt < ResendCooldown)
        {
            return ResendEmailVerificationResult.TooManyRequests;
        }

        var (token, hash, expiresAt) = EmailVerificationToken.Generate(now);
        await events.AppendAsync(userId, [new EmailVerificationRequested(userId, hash, expiresAt, now)], cancellationToken);

        await emailSender.SendEmailVerificationAsync(user.Email.Value, token, cancellationToken);

        return ResendEmailVerificationResult.Sent;
    }
}

public enum ResendEmailVerificationResult
{
    Sent,
    AlreadyVerified,
    TooManyRequests,
    UserNotFound
}
