using FluentValidation;

namespace buddy.Features.Users;

public sealed class UpdateEmailValidator : AbstractValidator<UpdateEmail>
{
    public UpdateEmailValidator()
    {
        RuleFor(x => x.Value).NotEmpty().EmailAddress().MaximumLength(200);
    }
}
