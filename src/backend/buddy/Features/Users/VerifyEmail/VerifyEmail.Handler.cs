using System.Security.Cryptography;
using System.Text;

using buddy.Common;
using buddy.Common.Validation;

using FluentValidation;

namespace buddy.Features.Users;

public static class VerifyEmailHandler
{
    public static async Task<Result<User>> Handle(
        VerifyEmail command,
        IValidator<VerifyEmail> validator,
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

        if (user.Email.IsVerified)
        {
            return new Result<User>.Success(user);
        }

        // These remain handler-side, unconverted checks -- they depend on the loaded user's
        // stored verification state (token hash, expiry), not just the command's own fields, so
        // they can't run as a pure FluentValidation rule the way the Token-required check above does.
        if (user.EmailVerificationTokenHash is null || user.EmailVerificationExpiresAt is null)
        {
            return new Result<User>.Validation(ValidationProblem.Of("The verification token is invalid."));
        }

        if (DateTimeOffset.UtcNow > user.EmailVerificationExpiresAt)
        {
            return new Result<User>.Validation(ValidationProblem.Of("The verification token has expired."));
        }

        var submittedHash = EmailVerificationToken.Hash(command.Token);

        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(submittedHash),
            Encoding.UTF8.GetBytes(user.EmailVerificationTokenHash)))
        {
            return new Result<User>.Validation(ValidationProblem.Of("The verification token is invalid."));
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
