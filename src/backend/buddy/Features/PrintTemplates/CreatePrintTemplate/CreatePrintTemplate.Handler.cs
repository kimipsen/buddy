using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Calendars;
using buddy.Features.Groups;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.PrintTemplates;

public static class CreatePrintTemplateHandler
{
    public static async Task<Result<PrintTemplateResponse>> Handle(
        CreatePrintTemplate command,
        IValidator<CreatePrintTemplate> validator,
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

        // Printing is a guardian activity; there is no child tier.
        if (await ChildVisibility.IsChildAsync(userId, guardians, cancellationToken))
        {
            return new Result<PrintTemplateResponse>.Forbidden();
        }

        // A group the caller isn't a guardian member of is indistinguishable from a missing one.
        if (command.GroupId is { } groupId
            && !await PrintTemplateAuthorization.IsGuardianMemberAsync(groupId, userId, groups, guardians, cancellationToken))
        {
            return new Result<PrintTemplateResponse>.NotFound();
        }

        var id = PrintTemplateId.New();
        var name = command.Name.Trim();
        var now = DateTimeOffset.UtcNow;

        PrintTemplateEvent created = command.GroupId is { } ownerGroupId
            ? new PrintTemplateCreatedForGroup(id, ownerGroupId, name, userId, now)
            : new PrintTemplateCreated(id, userId, name, userId, now);

        var template = PrintTemplate.Fold(null, created)!;

        await store.CreateAsync(id, [created], PrintTemplateIndexDocument.From(template), cancellationToken);

        return new Result<PrintTemplateResponse>.Success(PrintTemplateResponse.From(template));
    }
}
