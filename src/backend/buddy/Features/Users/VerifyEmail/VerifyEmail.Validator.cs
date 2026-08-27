using FluentValidation;

namespace buddy.Features.Users;

public sealed class VerifyEmailValidator : AbstractValidator<VerifyEmail>
{
    public VerifyEmailValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
    }
}
