using FluentValidation;

namespace buddy.Features.PrintTemplates;

public sealed class RenamePrintTemplateValidator : AbstractValidator<RenamePrintTemplate>
{
    public RenamePrintTemplateValidator()
    {
        this.ValidTemplateName(x => x.Name);
    }
}
