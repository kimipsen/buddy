using buddy.Common;
using buddy.Email;
using buddy.Features.Users;

namespace buddy.Features.Groups;

public static class InviteToGroupHandler
{
    // Mirrors ResendEmailVerificationHandler.ResendCooldown -- kept feature-local rather than
    // shared, consistent with how Users/Groups already don't reach into each other's handlers.
    public static readonly TimeSpan ResendCooldown = TimeSpan.FromMinutes(1);

    public static async Task<Result<GroupInviteSummary>> Handle(
        InviteToGroup command,
        IGroupEventStore groups,
        IUserEventStore users,
        IEmailSender emailSender,
        CancellationToken cancellationToken)
    {
        if (command.Role == GroupRole.Owner)
        {
            // Ownership is assigned only at creation, same restriction as SetGroupMemberRole.
            return new Result<GroupInviteSummary>.Forbidden();
        }

        if (command.UserId is not { } userId)
        {
            return new Result<GroupInviteSummary>.NotFound();
        }

        var events = await groups.ReadAsync(command.GroupId, cancellationToken);
        var group = Group.Rehydrate(events);
        var access = GroupAuthorization.CheckManage(group, userId);

        if (access != GroupAccess.Allowed)
        {
            return access.ToDeniedResult<GroupInviteSummary>();
        }

        // Deliberately no "does an account exist for this email" or "is this email already a
        // member" check here -- this codebase has no email-to-user lookup capability by design
        // (see the comment on GroupInviteCreated). AcceptGroupInvite is where the invited email
        // is ever compared against a real account, and only against the caller's own.
        var normalizedEmail = GroupInviteDocument.NormalizeEmail(command.Email);
        var now = DateTimeOffset.UtcNow;
        var existingInvite = await groups.FindPendingInviteAsync(command.GroupId, normalizedEmail, cancellationToken);

        if (existingInvite is not null && now - existingInvite.CreatedAt < ResendCooldown)
        {
            return new Result<GroupInviteSummary>.Validation("An invite was already sent recently. Try again in a minute.");
        }

        var inviteId = existingInvite?.Id ?? Guid.CreateVersion7();
        var (token, hash, expiresAt) = GroupInviteToken.Generate(now);

        await groups.AppendAsync(
            command.GroupId,
            [new GroupInviteCreated(command.GroupId, inviteId, normalizedEmail, command.Role, userId, hash, expiresAt, now)],
            cancellationToken);

        // Best-effort history entry on the inviter's own stream -- a separate store/schema from
        // Groups, so this isn't transactional with the append above (see the comment on
        // GroupInvitationSent).
        await users.AppendAsync(userId, [new GroupInvitationSent(userId, command.GroupId.Value, group!.Name, normalizedEmail, now)], cancellationToken);

        await emailSender.SendGroupInviteEmailAsync(normalizedEmail, group.Name, token, cancellationToken);

        return new Result<GroupInviteSummary>.Success(new GroupInviteSummary(inviteId, normalizedEmail, command.Role, now, expiresAt));
    }
}
