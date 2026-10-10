using FluentValidation;

namespace buddy.Features.HouseRules;

// Lengths and a non-blank title only: the body is markdown, and the backend never parses it
// (house-rules.md, Question 4). The 50-rule cap needs the book, so it's checked in the handler.
public sealed class AddRuleValidator : AbstractValidator<AddRule>
{
    public AddRuleValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(RuleContentRules.MaxTitleLength);
        RuleFor(x => x.Body).MaximumLength(RuleContentRules.MaxBodyLength);
    }
}

public static class RuleContentRules
{
    public const int MaxTitleLength = 100;

    // Same limit as UpdateSleepHygieneNotesValidator.MaxNotesLength.
    public const int MaxBodyLength = 4000;

    // Markdown is stored as typed, not trimmed like FreeText: leading indentation is a code block
    // and two trailing spaces are a line break. Only a body with nothing in it becomes "".
    public static string NormalizeBody(string? body) => string.IsNullOrWhiteSpace(body) ? "" : body;
}
