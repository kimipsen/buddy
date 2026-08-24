using buddy.Common;

namespace buddy.Features.Guardians;

public static class PreviewGuardianInviteHandler
{
    public static async Task<Result<GuardianInvitePreview>> Handle(PreviewGuardianInvite query, IGuardianInviteEventStore invites, CancellationToken cancellationToken)
    {
        var invite = await invites.FindInviteByTokenAsync(query.Token, cancellationToken);

        if (invite is null || invite.Status != GuardianInviteStatus.Pending || invite.ExpiresAt < DateTimeOffset.UtcNow)
        {
            return new Result<GuardianInvitePreview>.NotFound();
        }

        return new Result<GuardianInvitePreview>.Success(new GuardianInvitePreview(invite.ChildGivenName, invite.Kind));
    }
}
