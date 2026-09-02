using buddy.Common;
using buddy.Features.Users;

namespace buddy.Features.Guardians;

public static class AcceptGuardianInviteHandler
{
    public static async Task<Result<Unit>> Handle(
        AcceptGuardianInvite command,
        IGuardianInviteEventStore invites,
        IUserEventStore users,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (command.UserId is not { } userId)
        {
            return new Result<Unit>.NotFound();
        }

        var invite = await invites.FindInviteByTokenAsync(command.Token, cancellationToken);

        if (invite is null)
        {
            return new Result<Unit>.NotFound();
        }

        // Retrying an already-succeeded accept -- idempotent no-op instead of NotFound, so a
        // client retry after a dropped response doesn't read as failure. Only when this caller is
        // the one holding the resulting link; if it's since been revoked, that's a real absence
        // and falls through to NotFound below like any other non-Pending invite.
        if (invite.Status == GuardianInviteStatus.Accepted
            && await guardians.FindActiveLinkAsync(new UserId(invite.ChildId), userId, cancellationToken) is not null)
        {
            return new Result<Unit>.Success(Unit.Value);
        }

        if (invite.Status != GuardianInviteStatus.Pending || invite.ExpiresAt < DateTimeOffset.UtcNow)
        {
            return new Result<Unit>.NotFound();
        }

        // Self-scoped check, not a lookup of someone else -- the same reasoning as
        // AcceptGroupInviteHandler's identical check.
        var user = User.Rehydrate(await users.ReadAsync(userId, cancellationToken));

        if (user is null || user.IsDeleted)
        {
            return new Result<Unit>.NotFound();
        }

        if (!user.Email.IsVerified || GuardianInviteDocument.NormalizeEmail(user.Email.Value) != invite.InvitedEmail)
        {
            return new Result<Unit>.Forbidden();
        }

        var now = DateTimeOffset.UtcNow;
        var inviteId = new GuardianInviteId(invite.Id);
        var linkId = GuardianLinkId.New();
        var linked = new GuardianLinked(linkId, new UserId(invite.ChildId), userId, invite.Kind, now);

        await invites.AcceptAsync(
            inviteId,
            [new GuardianInviteAccepted(inviteId, userId, now)],
            linkId,
            [linked],
            cancellationToken);

        return new Result<Unit>.Success(Unit.Value);
    }
}
