using FluentValidation;

namespace buddy.Features.FlashCards;

public static class FlashCardRules
{
    public static IRuleBuilderOptions<T, string> ValidSetName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(FlashCardLimits.NameMaxLength);

    public static IRuleBuilderOptions<T, string> ValidCardText<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(FlashCardLimits.TextMaxLength);

    // The operation must be a defined one and the limit inside that operation's range.
    public static void ValidMathRule<T>(this IRuleBuilder<T, MathRule?> rule) =>
        rule.Must(m => m is null || (Enum.IsDefined(m.Operation) && MathDeck.IsValidLimit(m.Operation, m.Limit)))
            .WithMessage("math must name a valid operation and a limit in that operation's range.");
}
