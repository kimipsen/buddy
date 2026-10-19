using FluentValidation;

namespace buddy.Features.Users;

public sealed class UpdateNameValidator : AbstractValidator<UpdateName>
{
    public UpdateNameValidator()
    {
        RuleFor(x => x.Name.GivenName).PersonName().OverridePropertyName("givenName");
        RuleFor(x => x.Name.FamilyName).PersonName().OverridePropertyName("familyName");
    }
}
