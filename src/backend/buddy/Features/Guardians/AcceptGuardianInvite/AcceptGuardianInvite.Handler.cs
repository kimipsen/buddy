using buddy.Features.Users;

namespace buddy.Features.Guardians;

public static class AcceptGuardianInviteHandler
{
    public static async Task<AcceptGuardianInviteOutcome> Handle(
        AcceptGuardianInvite command,
        IGuardianInviteEventStore invites,
        IUserEventStore users,
        IGuardianLinkEventStore guardians,
        ILogger<AcceptGuardianInvite> logger,
        CancellationToken cancellationToken)
    {
        var userId = command.UserId;

        var invite = await invites.FindInviteByTokenAsync(command.Token, cancellationToken);

        if (invite is null)
        {
            return new AcceptGuardianInviteOutcome.NotFound();
        }

        // Retrying an already-succeeded accept -- idempotent no-op instead of NotFound, so a
        // client retry after a dropped response doesn't read as failure. Only when this caller is
        // the one holding the resulting link; if it's since been revoked, that's a real absence
        // and falls through to NotFound below like any other non-Pending invite.
        if (invite.Status == GuardianInviteStatus.Accepted
            && await guardians.FindActiveLinkAsync(new UserId(invite.ChildId), userId, cancellationToken) is not null)
        {
            return new AcceptGuardianInviteOutcome.Success();
        }

        if (invite.Status != GuardianInviteStatus.Pending || invite.ExpiresAt < DateTimeOffset.UtcNow)
        {
            return new AcceptGuardianInviteOutcome.NotFound();
        }

        // Self-scoped check, not a lookup of someone else -- the same reasoning as
        // AcceptGroupInviteHandler's identical check.
        var user = User.Rehydrate(await users.ReadAsync(userId, cancellationToken));

        if (user is null || user.IsDeleted)
        {
            return new AcceptGuardianInviteOutcome.NotFound();
        }

        if (GuardianInviteDocument.NormalizeEmail(user.Email.Value) != invite.InvitedEmail)
        {
            logger.GuardianInviteEmailMismatch(invite.Id, userId.Value);
            return new AcceptGuardianInviteOutcome.Forbidden();
        }

        if (!user.Email.IsVerified)
        {
            logger.GuardianInviteEmailMismatch(invite.Id, userId.Value);
            return new EmailNotVerified("Verify your email address before accepting this invite.");
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

        logger.GuardianInviteAccepted(invite.Id, userId.Value, invite.ChildId);

        return new AcceptGuardianInviteOutcome.Success();
    }
}
