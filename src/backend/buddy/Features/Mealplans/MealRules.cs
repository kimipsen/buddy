using buddy.Features.Calendars;

using FluentValidation;

namespace buddy.Features.Mealplans;

// Shared structural rules for a meal's details (CreateMeal, UpdateMealDetails and their ForGroup
// variants). The endpoints trim the name, description and color before they reach the command, so
// the limits apply to the trimmed values that get stored. Icon and color stay optional: the meal
// library form always sends both, but the API never required them.
internal static class MealRules
{
    public const int MaxNameLength = 200;
    public const int MaxDescriptionLength = 2000;

    // Same limit as WorkLocationRules.MaxColorLength and PrintTemplateRules.MaxColorLength.
    public const int MaxColorLength = 32;

    public static void ValidMealDetails<T>(
        this AbstractValidator<T> validator,
        Func<T, string> name,
        Func<T, string> description,
        Func<T, Icon> icon,
        Func<T, Color> color,
        Func<T, MealEatingOut> eatingOut)
    {
        validator.RuleFor(x => name(x)).NotEmpty().MaximumLength(MaxNameLength).OverridePropertyName("name");
        validator.RuleFor(x => description(x)).MaximumLength(MaxDescriptionLength).OverridePropertyName("description");
        validator.RuleFor(x => icon(x).Value).ValidIcon().OverridePropertyName("icon");
        // A pictogram name may be longer than an emoji (docs/frontend/analysis/pictograms.md).
        validator.RuleFor(x => icon(x).Value)
            .MaximumLength(Icon.MaxEmojiLength)
            .When(x => !icon(x).IsPictogram)
            .OverridePropertyName("icon");
        validator.RuleFor(x => color(x).Value).MaximumLength(MaxColorLength).OverridePropertyName("color");
        validator.RuleFor(x => eatingOut(x)).IsInEnum().OverridePropertyName("eatingOut");
    }
}
