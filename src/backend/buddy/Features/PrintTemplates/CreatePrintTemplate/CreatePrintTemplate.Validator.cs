using FluentValidation;

namespace buddy.Features.PrintTemplates;

public sealed class CreatePrintTemplateValidator : AbstractValidator<CreatePrintTemplate>
{
    public CreatePrintTemplateValidator()
    {
        this.ValidTemplateName(x => x.Name);
    }
}
