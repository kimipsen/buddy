using System.Security.Cryptography;
using System.Text;

using buddy.Common;

namespace buddy.Features.Users;

public static class VerifyEmailHandler
{
    public static async Task<Result<User>> Handle(VerifyEmail command, IUserEventStore events, CancellationToken cancellationToken)
    {
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

        if (user.Email.IsVerified)
        {
            return new Result<User>.Success(user);
        }

        if (user.EmailVerificationTokenHash is null || user.EmailVerificationExpiresAt is null)
        {
            return new Result<User>.Validation("The verification token is invalid.");
        }

        if (DateTimeOffset.UtcNow > user.EmailVerificationExpiresAt)
        {
            return new Result<User>.Validation("The verification token has expired.");
        }

        var submittedHash = EmailVerificationToken.Hash(command.Token);

        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(submittedHash),
            Encoding.UTF8.GetBytes(user.EmailVerificationTokenHash)))
        {
            return new Result<User>.Validation("The verification token is invalid.");
        }

        await events.AppendAsync(userId, [new EmailVerified(userId, DateTimeOffset.UtcNow)], cancellationToken);

        var verifiedUser = user with
        {
            Email = user.Email with { IsVerified = true },
            EmailVerificationTokenHash = null,
            EmailVerificationRequestedAt = null,
            EmailVerificationExpiresAt = null
        };

        return new Result<User>.Success(verifiedUser);
    }
}
