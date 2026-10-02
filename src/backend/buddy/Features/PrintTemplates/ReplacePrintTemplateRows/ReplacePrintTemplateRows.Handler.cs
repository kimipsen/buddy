using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Calendars;
using buddy.Features.Groups;
using buddy.Features.Guardians;
using buddy.Features.WorkLocations;

using FluentValidation;

namespace buddy.Features.PrintTemplates;

public static class ReplacePrintTemplateRowsHandler
{
    public static async Task<Result<PrintTemplateResponse>> Handle(
        ReplacePrintTemplateRows command,
        IValidator<ReplacePrintTemplateRows> validator,
        IPrintTemplateEventStore store,
        IGroupEventStore groups,
        IGuardianLinkEventStore guardians,
        ICalendarEventStore calendars,
        IWorkLocationScheduleEventStore workLocations,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<PrintTemplateResponse>.Validation(problem);
        }

        var userId = command.UserId;

        var (template, access) = await PrintTemplateLoader.LoadForManageAsync(store, command.TemplateId, userId, groups, guardians, cancellationToken);

        if (access != PrintTemplateAccess.Allowed)
        {
            return access.ToDeniedResult<PrintTemplateResponse>();
        }

        // Labels and filters are stored trimmed, so "unchanged" compares what would actually be saved.
        IReadOnlyList<PrintTemplateRow> after = [.. command.Rows.Select(row => row with { Label = row.Label.Trim(), TitleFilter = row.TitleFilter?.Trim() })];

        if (PrintTemplateRow.AreSame(after, template!.Rows))
        {
            return new Result<PrintTemplateResponse>.Success(PrintTemplateResponse.From(template));
        }

        var references = new PrintTemplateReferenceChecks(userId, guardians, groups, calendars, workLocations);

        if (await references.CheckRowsAsync(after, template.Rows, cancellationToken) is { } referenceError)
        {
            return new Result<PrintTemplateResponse>.Validation(ValidationProblem.Of(referenceError));
        }

        var replaced = new PrintTemplateRowsReplaced(template.Id, template.Rows, after, userId, DateTimeOffset.UtcNow);

        await store.AppendAsync(template.Id, [replaced], index: null, cancellationToken);

        return new Result<PrintTemplateResponse>.Success(PrintTemplateResponse.From(PrintTemplate.Fold(template, replaced)!));
    }
}
