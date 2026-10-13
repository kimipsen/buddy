using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.Feelings;

public static class RemoveFeelingCheckInHandler
{
    public static async Task<Result<Unit>> Handle(
        RemoveFeelingCheckIn command,
        IFeelingsJournalEventStore journals,
        IGuardianLinkEventStore guardians,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var access = await FeelingsAuthorization.CheckManage(command.ChildId, command.UserId, guardians, cancellationToken);

        if (access != FeelingsAccess.Allowed)
        {
            return access.ToDeniedResult<Unit>();
        }

        var id = FeelingsJournalId.ForChild(command.ChildId);
        var journal = FeelingsJournal.Rehydrate(await journals.ReadAsync(id, cancellationToken));

        // Already gone: an idempotent no-op. Any check-in may go, a self-report included (a sibling
        // grabbed the iPad); ModifiedBy records which guardian removed it.
        if (journal?.CheckIns.GetValueOrDefault(command.CheckInId) is not { } before)
        {
            return new Result<Unit>.Success(Unit.Value);
        }

        await journals.AppendAsync(id, [new FeelingCheckInRemoved(id, before, command.UserId, time.GetUtcNow())], cancellationToken);

        return new Result<Unit>.Success(Unit.Value);
    }
}
