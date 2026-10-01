using buddy.Common.RateLimiting;
using buddy.Email;

namespace buddy.Features.Groups;

public static class InviteToGroupHandler
{
    public static async Task<InviteToGroupOutcome> Handle(
        InviteToGroup command,
        IGroupEventStore groups,
        IEmailSender emailSender,
        CancellationToken cancellationToken)
    {
        if (command.Role == GroupRole.Owner)
        {
            // Ownership is assigned only at creation, same restriction as SetGroupMemberRole.
            return new InviteToGroupOutcome.Forbidden();
        }

        if (command.UserId is not { } userId)
        {
            return new InviteToGroupOutcome.NotFound();
        }

        var events = await groups.ReadAsync(command.GroupId, cancellationToken);
        var group = Group.Rehydrate(events);
        var access = GroupAuthorization.CheckManage(group, userId);

        if (access != GroupAccess.Allowed)
        {
            return access == GroupAccess.Forbidden ? new InviteToGroupOutcome.Forbidden() : new InviteToGroupOutcome.NotFound();
        }

        // Deliberately no "does an account exist for this email" or "is this email already a
        // member" check here -- this codebase has no email-to-user lookup capability by design
        // (see the comment on GroupInviteCreated). AcceptGroupInvite is where the invited email
        // is ever compared against a real account, and only against the caller's own.
        var normalizedEmail = GroupInviteDocument.NormalizeEmail(command.Email);
        var now = DateTimeOffset.UtcNow;
        var existingInvite = await groups.FindPendingInviteAsync(command.GroupId, normalizedEmail, cancellationToken);

        if (ResendCooldown.IsActive(existingInvite?.CreatedAt, now))
        {
            return new ResendCooldownActive("An invite was already sent recently. Try again in a minute.");
        }

        var inviteId = existingInvite?.Id ?? Guid.CreateVersion7();
        var (token, hash, expiresAt) = GroupInviteToken.Generate(now);

        await groups.AppendAsync(
            command.GroupId,
            [new GroupInviteCreated(command.GroupId, inviteId, normalizedEmail, command.Role, userId, hash, expiresAt, now)],
            cancellationToken);

        await emailSender.SendGroupInviteEmailAsync(normalizedEmail, group!.Name, token, cancellationToken);

        return new InviteToGroupOutcome.Success(new GroupInviteSummary(inviteId, normalizedEmail, command.Role, now, expiresAt));
    }
}
