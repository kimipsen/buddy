using FluentValidation;

namespace buddy.Features.Mealplans;

public sealed class SetProviderApiKeyValidator : AbstractValidator<SetProviderApiKey>
{
    public SetProviderApiKeyValidator()
    {
        RuleFor(x => x.Provider).IsInEnum();
        RuleFor(x => x.ApiKey).NotEmpty().MaximumLength(500);
    }
}
