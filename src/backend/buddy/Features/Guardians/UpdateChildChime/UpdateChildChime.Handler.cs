using buddy.Common;
using buddy.Features.Users;

namespace buddy.Features.Guardians;

public static class UpdateChildChimeHandler
{
    public static async Task<Result<ChildSummary>> Handle(
        UpdateChildChime command, IGuardianLinkEventStore guardianLinks, IUserEventStore users, TimeProvider time, CancellationToken cancellationToken)
    {
        // Collapsed to NotFound rather than Forbidden -- same "can't distinguish no-such-child from
        // not-your-child" precedent UpdateChildVisualTimersHandler follows for this exact lookup.
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

        if (child.AllowChime != command.AllowChime)
        {
            var updated = new ChimeAllowedUpdated(command.ChildId, child.AllowChime, command.AllowChime, time.GetUtcNow());
            await users.AppendAsync(command.ChildId, [updated], cancellationToken);
            child = child with { AllowChime = command.AllowChime };
        }

        return new Result<ChildSummary>.Success(ChildSummary.From(child, link));
    }
}
