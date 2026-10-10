using buddy.Features.Guardians;

namespace buddy.Features.Progress;

public static class ApproveRewardRequestHandler
{
    public static async Task<RewardRequestOutcome> Handle(
        ApproveRewardRequest command, IProgressEventStore progress, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        var access = await ProgressAuthorization.CheckManage(command.ChildId, command.UserId, guardians, cancellationToken);

        if (access != ProgressAccess.Allowed)
        {
            return access.ToDeniedOutcome();
        }

        return await RewardRequestResolution.ResolveAsync(
            command.ChildId,
            command.RequestId,
            RewardRequestStatus.Approved,
            (id, now) => new RewardRequestApproved(id, command.RequestId, command.UserId, now),
            progress,
            cancellationToken);
    }
}
