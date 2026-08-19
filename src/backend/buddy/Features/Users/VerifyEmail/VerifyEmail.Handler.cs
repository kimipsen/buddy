using System.Security.Cryptography;
using System.Text;

namespace buddy.Features.Users;

public static class VerifyEmailHandler
{
    public static async Task<VerifyEmailOutcome> Handle(VerifyEmail command, IUserEventStore events, CancellationToken cancellationToken)
    {
        if (command.UserId is not { } userId)
        {
            return new VerifyEmailOutcome(VerifyEmailResult.UserNotFound, null);
        }

        var existingEvents = await events.ReadAsync(userId, cancellationToken);
        var user = User.Rehydrate(existingEvents);

        if (user is null || user.IsDeleted)
        {
            return new VerifyEmailOutcome(VerifyEmailResult.UserNotFound, null);
        }

        if (user.Email.IsVerified)
        {
            return new VerifyEmailOutcome(VerifyEmailResult.AlreadyVerified, user);
        }

        if (user.EmailVerificationTokenHash is null || user.EmailVerificationExpiresAt is null)
        {
            return new VerifyEmailOutcome(VerifyEmailResult.InvalidToken, null);
        }

        if (DateTimeOffset.UtcNow > user.EmailVerificationExpiresAt)
        {
            return new VerifyEmailOutcome(VerifyEmailResult.Expired, null);
        }

        var submittedHash = EmailVerificationToken.Hash(command.Token);

        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(submittedHash),
            Encoding.UTF8.GetBytes(user.EmailVerificationTokenHash)))
        {
            return new VerifyEmailOutcome(VerifyEmailResult.InvalidToken, null);
        }

        await events.AppendAsync(userId, [new EmailVerified(userId, DateTimeOffset.UtcNow)], cancellationToken);

        var verifiedUser = user with
        {
            Email = user.Email with { IsVerified = true },
            EmailVerificationTokenHash = null,
            EmailVerificationRequestedAt = null,
            EmailVerificationExpiresAt = null
        };

        return new VerifyEmailOutcome(VerifyEmailResult.Verified, verifiedUser);
    }
}

public enum VerifyEmailResult
{
    Verified,
    AlreadyVerified,
    InvalidToken,
    Expired,
    UserNotFound
}

public sealed record VerifyEmailOutcome(VerifyEmailResult Result, User? User);
