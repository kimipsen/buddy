using FluentValidation;

namespace buddy.Features.Users;

// A person's given or family name (UpdateCurrentName, CreateChild): required and at most MaxLength
// characters. The endpoint trims it first, so a whitespace-only name is empty here and the trimmed
// value is what gets stored.
public static class NameRules
{
    public const int MaxLength = 200;

    public static IRuleBuilderOptions<T, string> PersonName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(MaxLength);
}
