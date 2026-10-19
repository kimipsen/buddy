using FluentValidation;

namespace buddy.Features.Groups;

public sealed class SetGroupMemberRoleValidator : AbstractValidator<SetGroupMemberRole>
{
    public SetGroupMemberRoleValidator()
    {
        // Ownership is assigned only at creation and never granted through this endpoint.
        RuleFor(x => x.Role).IsInEnum().NotEqual(GroupRole.Owner).WithMessage("Role must be Admin or Member.");
    }
}
