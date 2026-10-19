using FluentValidation;

namespace buddy.Features.Users;

public sealed class UpdateEmailValidator : AbstractValidator<UpdateEmail>
{
    public UpdateEmailValidator()
    {
        // The endpoint trims the address first, so these rules and the stored value see it trimmed.
        RuleFor(x => x.Value).NotEmpty().EmailAddress().MaximumLength(200).OverridePropertyName("email");
    }
}
