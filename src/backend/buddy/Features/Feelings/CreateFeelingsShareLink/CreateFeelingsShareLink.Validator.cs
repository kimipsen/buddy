using FluentValidation;

namespace buddy.Features.Feelings;

// ExpiresAt is optional ("no expiry" stays a deliberate guardian choice), but when given it must be
// in the future and within a year -- the sleep diary's rule.
public sealed class CreateFeelingsShareLinkValidator : AbstractValidator<CreateFeelingsShareLink>
{
    public CreateFeelingsShareLinkValidator(TimeProvider time)
    {
        RuleFor(x => x.ExpiresAt)
            .Must(expiresAt => expiresAt is null || expiresAt > time.GetUtcNow())
            .WithMessage("expiresAt must be in the future.");

        RuleFor(x => x.ExpiresAt)
            .Must(expiresAt => expiresAt is null || expiresAt <= time.GetUtcNow().AddDays(FeelingsRules.MaxShareLinkExpiryDays))
            .WithMessage($"expiresAt cannot be more than {FeelingsRules.MaxShareLinkExpiryDays} days away.");
    }
}
