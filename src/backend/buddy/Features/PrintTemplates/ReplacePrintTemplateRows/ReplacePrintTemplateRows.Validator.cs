using FluentValidation;

namespace buddy.Features.PrintTemplates;

// Structural rules only; whether the referenced children, groups, calendars and work locations are
// reachable needs the database and is PrintTemplateReferenceChecks' job in the handler.
public sealed class ReplacePrintTemplateRowsValidator : AbstractValidator<ReplacePrintTemplateRows>
{
    public ReplacePrintTemplateRowsValidator()
    {
        RuleFor(x => x.Rows)
            .Must(rows => rows.Count is > 0 and <= PrintTemplateRules.MaxRows)
            .WithMessage($"A print template needs 1–{PrintTemplateRules.MaxRows} rows.")
            .OverridePropertyName("rows");

        RuleForEach(x => x.Rows)
            .SetValidator(new PrintTemplateRowValidator())
            .OverridePropertyName("rows");
    }
}
