using FluentValidation;

namespace buddy.Features.PrintTemplates;

public sealed class UpdatePrintTemplateLayoutValidator : AbstractValidator<UpdatePrintTemplateLayout>
{
    public UpdatePrintTemplateLayoutValidator()
    {
        RuleFor(x => x.PaperSize).IsInEnum();
        RuleFor(x => x.DefaultStartWeekday).IsInEnum();
    }
}
