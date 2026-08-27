using buddy.Features.Users;

using FluentValidation;

namespace buddy.Features.Guardians;

public sealed class UpdateChildLanguageValidator : AbstractValidator<UpdateChildLanguage>
{
    public UpdateChildLanguageValidator()
    {
        RuleFor(x => x.Language)
            .Must(SupportedLanguages.IsValid)
            .WithMessage(x => $"'{x.Language.Value}' is not a supported language.");
    }
}
