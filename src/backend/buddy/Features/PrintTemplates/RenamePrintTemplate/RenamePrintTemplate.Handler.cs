using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Groups;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.PrintTemplates;

public static class RenamePrintTemplateHandler
{
    public static async Task<Result<PrintTemplateResponse>> Handle(
        RenamePrintTemplate command,
        IValidator<RenamePrintTemplate> validator,
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

        var name = command.Name.Trim();

        if (name == template.Name)
        {
            return new Result<PrintTemplateResponse>.Success(PrintTemplateResponse.From(template));
        }

        var renamed = new PrintTemplateRenamed(template.Id, template.Name, name, userId, DateTimeOffset.UtcNow);
        var after = PrintTemplate.Advance(template, renamed);

        await store.AppendAsync(template.Id, [renamed], PrintTemplateIndexDocument.From(after), cancellationToken);

        return new Result<PrintTemplateResponse>.Success(PrintTemplateResponse.From(after));
    }
}
