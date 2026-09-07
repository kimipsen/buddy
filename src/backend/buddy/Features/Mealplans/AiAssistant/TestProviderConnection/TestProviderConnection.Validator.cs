using FluentValidation;

namespace buddy.Features.Mealplans;

public sealed class TestProviderConnectionValidator : AbstractValidator<TestProviderConnection>
{
    public TestProviderConnectionValidator()
    {
        RuleFor(x => x.ApiKey).MaximumLength(500).When(x => x.ApiKey is not null);
    }
}
