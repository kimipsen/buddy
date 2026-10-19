using FluentValidation;

namespace buddy.Features.Mealplans;

public sealed class TestProviderConnectionValidator : AbstractValidator<TestProviderConnection>
{
    public TestProviderConnectionValidator()
    {
        // Minimal APIs bind {provider} with Enum.TryParse, which accepts any number, so "99"
        // reaches here as an undefined AiProvider (same rule as SetProviderApiKeyValidator).
        RuleFor(x => x.Provider).IsInEnum();
        RuleFor(x => x.ApiKey).MaximumLength(SetProviderApiKeyValidator.MaxApiKeyLength).When(x => x.ApiKey is not null);
    }
}
