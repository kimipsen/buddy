using buddy.Common;

namespace buddy.Features.Groups;

public static class PreviewGroupInviteHandler
{
    public static async Task<Result<GroupInvitePreview>> Handle(PreviewGroupInvite query, IGroupEventStore groups, CancellationToken cancellationToken)
    {
        var invite = await groups.FindInviteByTokenAsync(query.Token, cancellationToken);

        if (invite is null || invite.Status != GroupInviteStatus.Pending || invite.ExpiresAt < DateTimeOffset.UtcNow)
        {
            return new Result<GroupInvitePreview>.NotFound();
        }

        return new Result<GroupInvitePreview>.Success(new GroupInvitePreview(invite.GroupName));
    }
}
