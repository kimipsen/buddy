using buddy.Features.Users;

using FluentValidation;

namespace buddy.Features.Guardians;

public sealed class CreateChildValidator : AbstractValidator<CreateChild>
{
    public CreateChildValidator()
    {
        RuleFor(x => x.GivenName).PersonName();
        RuleFor(x => x.FamilyName).PersonName();
        RuleFor(x => x.Username).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Kind).IsInEnum();
    }
}
