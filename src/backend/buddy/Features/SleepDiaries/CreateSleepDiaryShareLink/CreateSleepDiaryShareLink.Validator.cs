using FluentValidation;

namespace buddy.Features.SleepDiaries;

// ExpiresAt is optional ("no expiry" stays a deliberate guardian choice), but when given it must be
// in the future and within a year -- the link is meant for a consultation, not a standing feed.
public sealed class CreateSleepDiaryShareLinkValidator : AbstractValidator<CreateSleepDiaryShareLink>
{
    public const int MaxExpiryDays = 365;

    public CreateSleepDiaryShareLinkValidator()
    {
        RuleFor(x => x.ExpiresAt)
            .Must(expiresAt => expiresAt is null || expiresAt > DateTimeOffset.UtcNow)
            .WithMessage("expiresAt must be in the future.");

        RuleFor(x => x.ExpiresAt)
            .Must(expiresAt => expiresAt is null || expiresAt <= DateTimeOffset.UtcNow.AddDays(MaxExpiryDays))
            .WithMessage($"expiresAt cannot be more than {MaxExpiryDays} days away.");
    }
}
