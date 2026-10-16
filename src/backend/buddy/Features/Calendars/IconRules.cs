using System.Text.RegularExpressions;

using FluentValidation;

namespace buddy.Features.Calendars;

// The structural rule on an icon, shared by the calendar item, task library and First-Then
// validators. Only the shape of a pictogram is checked, not whether the frontend ships that file:
// the frontend owns the set and renders a fallback for an unknown name
// (docs/frontend/analysis/pictograms.md, Question 2).
public static partial class IconRules
{
    public const string PictogramMessage =
        "A pictogram icon must be 'pictogram:' followed by a lowercase, hyphenated name.";

    public static IRuleBuilderOptions<T, string> ValidIcon<T>(this IRuleBuilder<T, string> rule) =>
        rule.MaximumLength(Icon.MaxLength)
            .Must(IsEmojiOrPictogram)
            .WithMessage(PictogramMessage);

    private static bool IsEmojiOrPictogram(string value) =>
        !value.StartsWith(Icon.PictogramPrefix, StringComparison.Ordinal)
        || Slug().IsMatch(value[Icon.PictogramPrefix.Length..]);

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*\\z", RegexOptions.CultureInvariant)]
    private static partial Regex Slug();
}
