using FluentValidation;

namespace buddy.Features.Users;

public sealed class UpdateCallingNameValidator : AbstractValidator<UpdateCallingName>
{
    public const int MaxLength = 50;

    public UpdateCallingNameValidator()
    {
        // Empty is allowed: it clears the calling name, and children see the given name again.
        RuleFor(x => x.CallingName).MaximumLength(MaxLength);
    }
}
