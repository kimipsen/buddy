using buddy.Common;
using buddy.Features.Users;

namespace buddy.Features.Guardians;

public static class AcceptGuardianInviteHandler
{
    public static async Task<Result<Unit>> Handle(
        AcceptGuardianInvite command, IGuardianInviteEventStore invites, IUserEventStore users, CancellationToken cancellationToken)
    {
        if (command.UserId is not { } userId)
        {
            return new Result<Unit>.NotFound();
        }

        var invite = await invites.FindInviteByTokenAsync(command.Token, cancellationToken);

        if (invite is null || invite.Status != GuardianInviteStatus.Pending || invite.ExpiresAt < DateTimeOffset.UtcNow)
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
