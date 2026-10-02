using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Calendars;
using buddy.Features.Groups;
using buddy.Features.Guardians;
using buddy.Features.WorkLocations;

using FluentValidation;

namespace buddy.Features.PrintTemplates;

public static class ReplacePrintTemplateGuardianColorsHandler
{
    public static async Task<Result<PrintTemplateResponse>> Handle(
        ReplacePrintTemplateGuardianColors command,
        IValidator<ReplacePrintTemplateGuardianColors> validator,
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

        if (command.UserId is not { } userId)
        {
            return new Result<PrintTemplateResponse>.NotFound();
        }

        var (template, access) = await PrintTemplateLoader.LoadForManageAsync(store, command.TemplateId, userId, groups, guardians, cancellationToken);

        if (access != PrintTemplateAccess.Allowed)
        {
            return access.ToDeniedResult<PrintTemplateResponse>();
        }

        if (command.Colors.SequenceEqual(template!.GuardianColors))
        {
            return new Result<PrintTemplateResponse>.Success(PrintTemplateResponse.From(template));
        }

        var references = new PrintTemplateReferenceChecks(userId, guardians, groups, calendars, workLocations);

        if (await references.CheckGuardianColorsAsync(command.Colors, template.GuardianColors, cancellationToken) is { } referenceError)
        {
            return new Result<PrintTemplateResponse>.Validation(ValidationProblem.Of(referenceError));
        }

        var replaced = new PrintTemplateGuardianColorsReplaced(template.Id, template.GuardianColors, command.Colors, userId, DateTimeOffset.UtcNow);

        await store.AppendAsync(template.Id, [replaced], index: null, cancellationToken);

        return new Result<PrintTemplateResponse>.Success(PrintTemplateResponse.From(PrintTemplate.Fold(template, replaced)!));
    }
}
