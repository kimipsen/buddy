using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.Users;

public static class VerifyEmailHandler
{
    public static async Task<Result<User>> Handle(
        VerifyEmail command,
        IValidator<VerifyEmail> validator,
        IUserEventStore events,
        IKeycloakAdminClient keycloak,
        ILogger<VerifyEmail> logger,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<User>.Validation(problem);
        }

        var userId = command.UserId;

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
        if (user.EmailVerification is not EmailVerification.Pending pending)
        {
            logger.EmailVerificationRejected(userId.Value, "no verification pending");
            return new Result<User>.Validation(ValidationProblem.Of("The verification token is invalid."));
        }

        if (DateTimeOffset.UtcNow > pending.ExpiresAt)
        {
            logger.EmailVerificationRejected(userId.Value, "token expired");
            return new Result<User>.Validation(ValidationProblem.Of("The verification token has expired."));
        }

        var submittedHash = EmailVerificationToken.Hash(command.Token);

        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(submittedHash),
            Encoding.UTF8.GetBytes(pending.TokenHash)))
        {
            logger.EmailVerificationRejected(userId.Value, "token mismatch");
            return new Result<User>.Validation(ValidationProblem.Of("The verification token is invalid."));
        }

        await events.AppendAsync(userId, [new EmailVerified(userId, DateTimeOffset.UtcNow)], cancellationToken);

        logger.EmailVerified(userId.Value);

        // Best effort: Buddy's EmailVerified event is what Buddy acts on (invites check it), so a
        // Keycloak outage mustn't fail a verification that has already been recorded.
        try
        {
            await keycloak.MarkEmailVerifiedAsync(user.KeycloakSubject, user.Email.Value, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException
            || (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.KeycloakEmailVerifiedSyncFailed(exception, userId.Value);
        }

        var verifiedUser = user with { Email = user.Email with { IsVerified = true }, EmailVerification = new EmailVerification.None() };

        return new Result<User>.Success(verifiedUser);
    }
}
