using FluentValidation;

namespace buddy.Features.Guardians;

public sealed class CreateChildValidator : AbstractValidator<CreateChild>
{
    public CreateChildValidator()
    {
        RuleFor(x => x.GivenName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.FamilyName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Username).NotEmpty().MaximumLength(200);
    }
}
