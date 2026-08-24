using buddy.Common;

namespace buddy.Features.Guardians;

public static class RevokeGuardianInviteHandler
{
    public static async Task<Result<Unit>> Handle(
        RevokeGuardianInvite command, IGuardianLinkEventStore guardianLinks, IGuardianInviteEventStore invites, CancellationToken cancellationToken)
    {
        if (command.UserId is not { } userId)
        {
            return new Result<Unit>.NotFound();
        }

        // Any active guardian of this child can revoke a pending invite -- no Owner/Admin split,
        // same reasoning as InviteGuardianHandler.
        var link = await guardianLinks.FindActiveLinkAsync(command.ChildId, userId, cancellationToken);

        if (link is null)
        {
            return new Result<Unit>.NotFound();
        }

        var invite = await invites.FindInviteAsync(command.InviteId, cancellationToken);

        if (invite is null)
        {
            // Nothing to revoke -- same idempotent-delete convention as RevokeGroupInvite.
            return new Result<Unit>.Success(Unit.Value);
        }

        if (invite.ChildId != command.ChildId.Value)
        {
            // Belongs to a different child -- don't confirm or deny it exists elsewhere.
            return new Result<Unit>.NotFound();
        }

        if (invite.Status != GuardianInviteStatus.Pending)
        {
            return new Result<Unit>.Success(Unit.Value);
        }

        await invites.AppendAsync(new GuardianInviteId(invite.Id), [new GuardianInviteRevoked(new GuardianInviteId(invite.Id), userId, DateTimeOffset.UtcNow)], cancellationToken);

        return new Result<Unit>.Success(Unit.Value);
    }
}
