using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Users;

using FluentValidation;

namespace buddy.Features.Guardians;

public static class UpdateChildLanguageHandler
{
    public static async Task<Result<ChildSummary>> Handle(
        UpdateChildLanguage command, IValidator<UpdateChildLanguage> validator, IGuardianLinkEventStore guardianLinks, IUserEventStore users, CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<ChildSummary>.Validation(problem);
        }

        if (command.GuardianId is not { } guardianId)
        {
            return new Result<ChildSummary>.NotFound();
        }

        // Collapsed to NotFound rather than Forbidden -- same "can't distinguish no-such-child from
        // not-your-child" precedent RevokeGuardianLinkHandler follows for this exact lookup.
        var link = await guardianLinks.FindActiveLinkAsync(command.ChildId, guardianId, cancellationToken);

        if (link is null)
        {
            return new Result<ChildSummary>.NotFound();
        }

        var existingEvents = await users.ReadAsync(command.ChildId, cancellationToken);
        var child = User.Rehydrate(existingEvents);

        if (child is null || child.IsDeleted)
        {
            return new Result<ChildSummary>.NotFound();
        }

        if (child.ResolvedLanguage != command.Language)
        {
            var languageUpdated = new LanguageUpdated(command.ChildId, child.ResolvedLanguage, command.Language, DateTimeOffset.UtcNow);
            await users.AppendAsync(command.ChildId, [languageUpdated], cancellationToken);
            child = child with { Language = command.Language };
        }

        return new Result<ChildSummary>.Success(new ChildSummary(
            child.Id, child.Name, new GuardianLinkId(link.GuardianLinkId), link.Kind, child.ResolvedLanguage, child.ResolvedTimeZoneId));
    }
}
