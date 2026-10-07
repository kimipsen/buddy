using buddy.Features.Users;

namespace buddy.Features.Groups;

public static class AcceptGroupInviteHandler
{
    public static async Task<AcceptGroupInviteOutcome> Handle(AcceptGroupInvite command, IGroupEventStore groups, IUserEventStore users, ILogger<AcceptGroupInvite> logger, CancellationToken cancellationToken)
    {
        var userId = command.UserId;

        var invite = await groups.FindInviteByTokenAsync(command.Token, cancellationToken);

        if (invite is null)
        {
            return new AcceptGroupInviteOutcome.NotFound();
        }

        // Retrying an already-succeeded accept -- idempotent no-op instead of NotFound, so a
        // client retry after a dropped response doesn't read as failure. Only when this caller is
        // still a member; if they've since been removed, that's a real absence and falls through
        // to NotFound below like any other non-Pending invite.
        if (invite.Status == GroupInviteStatus.Accepted)
        {
            var existingGroup = Group.Rehydrate(await groups.ReadAsync(new GroupId(invite.GroupId), cancellationToken));

            if (existingGroup is not null && existingGroup.Members.ContainsKey(userId))
            {
                return new AcceptGroupInviteOutcome.Success();
            }
        }

        if (invite.Status != GroupInviteStatus.Pending || invite.ExpiresAt < DateTimeOffset.UtcNow)
        {
            return new AcceptGroupInviteOutcome.NotFound();
        }

        // Self-scoped check, not a lookup of someone else -- reads the caller's own User record
        // (the same thing GET /users/me does) and compares it to the invited email. This is how
        // an invite is ever tied to a real account: never by resolving InvitedEmail to a UserId
        // ahead of time. See the comment on GroupInviteCreated for why.
        var userEvents = await users.ReadAsync(userId, cancellationToken);
        var user = User.Rehydrate(userEvents);

        if (user is null || user.IsDeleted)
        {
            return new AcceptGroupInviteOutcome.NotFound();
        }

        if (GroupInviteDocument.NormalizeEmail(user.Email.Value) != invite.InvitedEmail)
        {
            logger.GroupInviteEmailMismatch(invite.Id, userId.Value);
            return new AcceptGroupInviteOutcome.Forbidden();
        }

        // The right address, but unverified -- it could be claimed by someone other than its real
        // owner, so it can't be trusted to accept an invite that was sent to it. The invite stays
        // pending: once the caller verifies the address, the same link works.
        if (!user.Email.IsVerified)
        {
            logger.GroupInviteEmailMismatch(invite.Id, userId.Value);
            return new EmailNotVerified("Verify your email address before accepting this invite.");
        }

        var groupId = new GroupId(invite.GroupId);
        var groupEvents = await groups.ReadAsync(groupId, cancellationToken);
        var group = Group.Rehydrate(groupEvents);

        if (group is null || group.IsDeleted)
        {
            return new AcceptGroupInviteOutcome.NotFound();
        }

        var now = DateTimeOffset.UtcNow;

        await groups.AppendAsync(
            groupId,
            [
                // GrantedBy is the inviter, not the accepting user -- it records who authorized
                // this membership, matching SetGroupMemberRole's use of the same field for the
                // acting admin.
                new GroupMemberRoleGranted(groupId, userId, invite.Role, new UserId(invite.InvitedBy), now),
                new GroupInviteAccepted(groupId, invite.Id, userId, now)
            ],
            cancellationToken);

        logger.GroupInviteAccepted(invite.Id, userId.Value, invite.Role, groupId.Value);

        return new AcceptGroupInviteOutcome.Success();
    }
}
