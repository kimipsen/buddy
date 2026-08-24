using buddy.Common;
using buddy.Features.Users;

namespace buddy.Features.Groups;

public static class AcceptGroupInviteHandler
{
    public static async Task<Result<Unit>> Handle(AcceptGroupInvite command, IGroupEventStore groups, IUserEventStore users, CancellationToken cancellationToken)
    {
        if (command.UserId is not { } userId)
        {
            return new Result<Unit>.NotFound();
        }

        var invite = await groups.FindInviteByTokenAsync(command.Token, cancellationToken);

        if (invite is null || invite.Status != GroupInviteStatus.Pending || invite.ExpiresAt < DateTimeOffset.UtcNow)
        {
            return new Result<Unit>.NotFound();
        }

        // Self-scoped check, not a lookup of someone else -- reads the caller's own User record
        // (the same thing GET /users/me does) and compares it to the invited email. This is how
        // an invite is ever tied to a real account: never by resolving InvitedEmail to a UserId
        // ahead of time. See the comment on GroupInviteCreated for why.
        var userEvents = await users.ReadAsync(userId, cancellationToken);
        var user = User.Rehydrate(userEvents);

        if (user is null || user.IsDeleted)
        {
            return new Result<Unit>.NotFound();
        }

        if (!user.Email.IsVerified || GroupInviteDocument.NormalizeEmail(user.Email.Value) != invite.InvitedEmail)
        {
            // Covers both a mismatched email and an unverified one -- an unverified address could
            // be claimed by someone other than its real owner, so it can't be trusted to accept an
            // invite that was sent to it.
            return new Result<Unit>.Forbidden();
        }

        var groupId = new GroupId(invite.GroupId);
        var groupEvents = await groups.ReadAsync(groupId, cancellationToken);
        var group = Group.Rehydrate(groupEvents);

        if (group is null || group.IsDeleted)
        {
            return new Result<Unit>.NotFound();
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

        return new Result<Unit>.Success(Unit.Value);
    }
}
