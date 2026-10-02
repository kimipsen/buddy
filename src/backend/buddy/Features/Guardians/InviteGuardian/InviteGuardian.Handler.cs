using buddy.Common.RateLimiting;
using buddy.Email;
using buddy.Features.Users;

namespace buddy.Features.Guardians;

public static class InviteGuardianHandler
{
    public static async Task<InviteGuardianOutcome> Handle(
        InviteGuardian command,
        IGuardianLinkEventStore guardianLinks,
        IGuardianInviteEventStore invites,
        IUserEventStore users,
        IEmailSender emailSender,
        CancellationToken cancellationToken)
    {
        var userId = command.UserId;

        // Any active guardian of this child can invite a co-guardian -- GuardianKind never gates
        // access, so there's no Owner/Admin-style split the way Groups' invite has.
        var link = await guardianLinks.FindActiveLinkAsync(command.ChildId, userId, cancellationToken);

        if (link is null)
        {
            return new InviteGuardianOutcome.NotFound();
        }

        var child = User.Rehydrate(await users.ReadAsync(command.ChildId, cancellationToken));

        if (child is null || child.IsDeleted)
        {
            return new InviteGuardianOutcome.NotFound();
        }

        // Deliberately no "does an account exist for this email" check -- same
        // no-email-to-user-lookup design as InviteToGroupHandler.
        var normalizedEmail = GuardianInviteDocument.NormalizeEmail(command.Email);
        var now = DateTimeOffset.UtcNow;
        var existingInvite = await invites.FindPendingInviteAsync(command.ChildId, normalizedEmail, cancellationToken);

        if (ResendCooldown.IsActive(existingInvite?.CreatedAt, now))
        {
            return new ResendCooldownActive("An invite was already sent recently. Try again in a minute.");
        }

        var inviteId = existingInvite is null ? GuardianInviteId.New() : new GuardianInviteId(existingInvite.Id);
        var (token, hash, expiresAt) = GuardianInviteToken.Generate(now);
        var created = new GuardianInviteCreated(inviteId, command.ChildId, child.Name.GivenName, normalizedEmail, command.Kind, userId, hash, expiresAt, now);

        if (existingInvite is null)
        {
            await invites.CreateAsync(inviteId, [created], cancellationToken);
        }
        else
        {
            await invites.AppendAsync(inviteId, [created], cancellationToken);
        }

        await emailSender.SendGuardianInviteEmailAsync(normalizedEmail, child.Name.GivenName, token, cancellationToken);

        return new InviteGuardianOutcome.Success(new GuardianInviteSummary(inviteId.Value, normalizedEmail, command.Kind, now, expiresAt));
    }
}
