using buddy.Common;
using buddy.Common.Validation;
using buddy.Email;

using FluentValidation;

namespace buddy.Features.Users;

public static class UpdateEmailHandler
{
    public static async Task<Result<User>> Handle(
        UpdateEmail command,
        IValidator<UpdateEmail> validator,
        IUserEventStore events,
        IEmailSender emailSender,
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

        if (user.Email.Value == command.Value)
        {
            return new Result<User>.Success(user);
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

        return new Result<User>.Success(updated);
    }
}
