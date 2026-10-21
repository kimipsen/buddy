using FluentValidation;

namespace buddy.Features.Mealplans;

public sealed class ConnectMealieValidator : AbstractValidator<ConnectMealie>
{
    // Mealie's long-lived tokens are JWTs of a few hundred characters. The floor keeps
    // StoredApiKey.Last4 a strict suffix, as for AI keys.
    public const int MinApiTokenLength = 8;
    public const int MaxApiTokenLength = 2000;

    public ConnectMealieValidator()
    {
        RuleFor(x => x.BaseUrl).NotEmpty().MaximumLength(MealieOutboundPolicy.MaxBaseUrlLength);
        RuleFor(x => x.ApiToken).NotEmpty().MinimumLength(MinApiTokenLength).MaximumLength(MaxApiTokenLength);
    }
}
