using FluentValidation;

namespace buddy.Features.VisitSummaries;

// ExpiresAt is optional ("no expiry" stays a deliberate guardian choice), but when given it must be
// in the future and within a year, as the sleep diary's links.
public sealed class CreateVisitSummaryShareLinkValidator : AbstractValidator<CreateVisitSummaryShareLink>
{
    public CreateVisitSummaryShareLinkValidator(TimeProvider time)
    {
        RuleFor(x => x.ExpiresAt)
            .Must(expiresAt => expiresAt is null || expiresAt > time.GetUtcNow())
            .WithMessage("expiresAt must be in the future.");

        RuleFor(x => x.ExpiresAt)
            .Must(expiresAt => expiresAt is null || expiresAt <= time.GetUtcNow().AddDays(VisitSummaryRules.MaxShareLinkExpiryDays))
            .WithMessage($"expiresAt cannot be more than {VisitSummaryRules.MaxShareLinkExpiryDays} days away.");
    }
}
