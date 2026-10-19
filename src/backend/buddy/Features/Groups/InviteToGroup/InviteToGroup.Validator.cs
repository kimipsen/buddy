using FluentValidation;

namespace buddy.Features.Groups;

public sealed class InviteToGroupValidator : AbstractValidator<InviteToGroup>
{
    // RFC 5321's limit on a deliverable address.
    public const int EmailMaxLength = 254;

    public InviteToGroupValidator()
    {
        // Checked as the handler stores and emails it: trimmed and lower-cased.
        RuleFor(x => GroupInviteDocument.NormalizeEmail(x.Email))
            .NotEmpty().EmailAddress().MaximumLength(EmailMaxLength).OverridePropertyName("email");

        // Ownership is assigned only at creation, same rule as SetGroupMemberRole.
        RuleFor(x => x.Role).IsInEnum().NotEqual(GroupRole.Owner).WithMessage("Role must be Admin or Member.");
    }
}
