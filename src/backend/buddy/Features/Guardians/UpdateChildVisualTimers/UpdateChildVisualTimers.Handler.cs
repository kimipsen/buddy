using buddy.Common;
using buddy.Features.Users;

namespace buddy.Features.Guardians;

public static class UpdateChildVisualTimersHandler
{
    public static async Task<Result<ChildSummary>> Handle(
        UpdateChildVisualTimers command, IGuardianLinkEventStore guardianLinks, IUserEventStore users, CancellationToken cancellationToken)
    {
        // Collapsed to NotFound rather than Forbidden -- same "can't distinguish no-such-child from
        // not-your-child" precedent UpdateChildLanguageHandler follows for this exact lookup.
        var link = await guardianLinks.FindActiveLinkAsync(command.ChildId, command.GuardianId, cancellationToken);

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

        if (child.ShowVisualTimers != command.ShowVisualTimers)
        {
            var updated = new ShowVisualTimersUpdated(command.ChildId, child.ShowVisualTimers, command.ShowVisualTimers, DateTimeOffset.UtcNow);
            await users.AppendAsync(command.ChildId, [updated], cancellationToken);
            child = child with { ShowVisualTimers = command.ShowVisualTimers };
        }

        return new Result<ChildSummary>.Success(new ChildSummary(
            child.Id, child.Name, new GuardianLinkId(link.GuardianLinkId), link.Kind, child.Language, child.TimeZoneId, child.ShowVisualTimers));
    }
}
