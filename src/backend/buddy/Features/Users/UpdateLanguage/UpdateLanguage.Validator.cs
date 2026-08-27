using FluentValidation;

namespace buddy.Features.Users;

public sealed class UpdateLanguageValidator : AbstractValidator<UpdateLanguage>
{
    public UpdateLanguageValidator()
    {
        RuleFor(x => x.Language)
            .Must(SupportedLanguages.IsValid)
            .WithMessage(x => $"'{x.Language.Value}' is not a supported language.");
    }
}
