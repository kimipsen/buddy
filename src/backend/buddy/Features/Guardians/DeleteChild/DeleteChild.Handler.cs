using buddy.Features.Privacy;
using buddy.Features.Users;

namespace buddy.Features.Guardians;

public static class DeleteChildHandler
{
    public static async Task<DeleteChildOutcome> Handle(
        DeleteChild command,
        IGuardianLinkEventStore guardianLinks,
        IUserEventStore users,
        UserErasure erasure,
        ILogger<DeleteChild> logger,
        CancellationToken cancellationToken)
    {
        if (await guardianLinks.FindActiveLinkAsync(command.ChildId, command.GuardianId, cancellationToken) is null
            || await users.FindSnapshotAsync(command.ChildId, cancellationToken) is not { IsDeleted: false } child)
        {
            return new DeleteChildOutcome.NotFound();
        }

        if (await erasure.HasOtherGuardianAsync(command.ChildId, command.GuardianId, cancellationToken))
        {
            return new DeleteChildOutcome.HasOtherGuardians();
        }

        await erasure.DeleteChildAsync(child, cancellationToken);

        logger.ChildDeleted(command.ChildId.Value, command.GuardianId.Value);

        return new DeleteChildOutcome.Success();
    }
}
