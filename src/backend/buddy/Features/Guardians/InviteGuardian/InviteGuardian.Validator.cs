using FluentValidation;

namespace buddy.Features.Guardians;

public sealed class InviteGuardianValidator : AbstractValidator<InviteGuardian>
{
    // RFC 5321's limit on a deliverable address.
    public const int EmailMaxLength = 254;

    public InviteGuardianValidator()
    {
        // Checked as the handler stores and emails it: trimmed and lower-cased.
        RuleFor(x => GuardianInviteDocument.NormalizeEmail(x.Email))
            .NotEmpty().EmailAddress().MaximumLength(EmailMaxLength).OverridePropertyName("email");
        RuleFor(x => x.Kind).IsInEnum();
    }
}
