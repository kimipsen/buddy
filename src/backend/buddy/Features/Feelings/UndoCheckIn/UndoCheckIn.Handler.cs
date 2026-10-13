using buddy.Features.Guardians;

namespace buddy.Features.Feelings;

public static class UndoCheckInHandler
{
    public static async Task<UndoCheckInOutcome> Handle(
        UndoCheckIn command,
        IFeelingsJournalEventStore journals,
        IGuardianLinkEventStore guardians,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await FeelingsAuthorization.CheckSelf(command.UserId, guardians, cancellationToken) != FeelingsAccess.Allowed)
        {
            return new UndoCheckInOutcome.NotFound();
        }

        var id = FeelingsJournalId.ForChild(command.UserId);
        var journal = FeelingsJournal.Rehydrate(await journals.ReadAsync(id, cancellationToken));

        // Already gone (or never there): an idempotent no-op, as a repeated DELETE should be.
        if (journal?.CheckIns.GetValueOrDefault(command.CheckInId) is not { } before)
        {
            return new UndoCheckInOutcome.Success();
        }

        // The /me routes only see the child's own self-reports; a guardian's observation is as
        // invisible here as one that doesn't exist.
        if (!before.IsSelfReportOf(command.UserId))
        {
            return new UndoCheckInOutcome.NotFound();
        }

        var now = time.GetUtcNow();

        if (now - before.RecordedAt > FeelingsRules.UndoWindow)
        {
            return new UndoCheckInOutcome.UndoWindowClosed();
        }

        await journals.AppendAsync(id, [new FeelingCheckInRemoved(id, before, command.UserId, now)], cancellationToken);

        return new UndoCheckInOutcome.Success();
    }
}
