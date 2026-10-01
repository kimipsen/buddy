using buddy.Common.RateLimiting;
using buddy.Email;

namespace buddy.Features.Users;

public static class ResendEmailVerificationHandler
{
    public static async Task<ResendEmailVerificationOutcome> Handle(ResendEmailVerification command, IUserEventStore events, IEmailSender emailSender, CancellationToken cancellationToken)
    {
        if (command.UserId is not { } userId)
        {
            return new ResendEmailVerificationOutcome.NotFound();
        }

        var existingEvents = await events.ReadAsync(userId, cancellationToken);
        var user = User.Rehydrate(existingEvents);

        if (user is null || user.IsDeleted)
        {
            return new ResendEmailVerificationOutcome.NotFound();
        }

        if (user.Email.IsVerified)
        {
            return new ResendEmailVerificationOutcome.AlreadyVerified();
        }

        var now = DateTimeOffset.UtcNow;

        if (ResendCooldown.IsActive(user.EmailVerificationRequestedAt, now))
        {
            return new ResendCooldownActive("A verification email was already sent recently. Try again in a minute.");
        }

        var (token, hash, expiresAt) = EmailVerificationToken.Generate(now);
        await events.AppendAsync(userId, [new EmailVerificationRequested(userId, hash, expiresAt, now)], cancellationToken);

        await emailSender.SendEmailVerificationAsync(user.Email.Value, token, cancellationToken);

        return new ResendEmailVerificationOutcome.Sent();
    }
}

// Its own outcome rather than Result<T>: AlreadyVerified is a success (204) with nothing to
// return, and the cooldown is the shared ResendCooldownActive (409, see ResendCooldown).
public union ResendEmailVerificationOutcome(
    ResendEmailVerificationOutcome.Sent,
    ResendEmailVerificationOutcome.AlreadyVerified,
    ResendEmailVerificationOutcome.NotFound,
    ResendCooldownActive)
{
    public sealed record Sent;
    public sealed record AlreadyVerified;
    public sealed record NotFound;
}
