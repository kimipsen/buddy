using FluentValidation;

namespace buddy.Features.Mealplans;

public sealed class SetProviderApiKeyValidator : AbstractValidator<SetProviderApiKey>
{
    // No provider issues keys this short. The floor also keeps StoredApiKey.Last4 a strict suffix,
    // so the settings UI never shows a whole key back.
    public const int MinApiKeyLength = 8;
    public const int MaxApiKeyLength = 500;

    public SetProviderApiKeyValidator()
    {
        RuleFor(x => x.Provider).IsInEnum();
        // The endpoint trims the key before it reaches the command.
        RuleFor(x => x.ApiKey).NotEmpty().MinimumLength(MinApiKeyLength).MaximumLength(MaxApiKeyLength);
    }
}
