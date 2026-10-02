using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Groups;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.PrintTemplates;

public static class UpdatePrintTemplateLayoutHandler
{
    public static async Task<Result<PrintTemplateResponse>> Handle(
        UpdatePrintTemplateLayout command,
        IValidator<UpdatePrintTemplateLayout> validator,
        IPrintTemplateEventStore store,
        IGroupEventStore groups,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<PrintTemplateResponse>.Validation(problem);
        }

        var userId = command.UserId;

        var loaded = await PrintTemplateLoader.LoadForManageAsync(store, command.TemplateId, userId, groups, guardians, cancellationToken);

        if (loaded is not Result<PrintTemplate>.Success(var template))
        {
            return loaded.Reraise<PrintTemplate, PrintTemplateResponse>();
        }

        if (template.PaperSize == command.PaperSize
            && template.DefaultStartWeekday == command.DefaultStartWeekday
            && template.ShowWeekNumber == command.ShowWeekNumber)
        {
            return new Result<PrintTemplateResponse>.Success(PrintTemplateResponse.From(template));
        }

        var changed = new PrintTemplateLayoutChanged(
            template.Id,
            template.PaperSize, command.PaperSize,
            template.DefaultStartWeekday, command.DefaultStartWeekday,
            template.ShowWeekNumber, command.ShowWeekNumber,
            userId,
            DateTimeOffset.UtcNow);

        await store.AppendAsync(template.Id, [changed], index: null, cancellationToken);

        return new Result<PrintTemplateResponse>.Success(PrintTemplateResponse.From(PrintTemplate.Advance(template, changed)));
    }
}
