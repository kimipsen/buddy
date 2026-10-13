using buddy.Common.Validation;
using buddy.Features.Guardians;
using buddy.Features.Users;

using FluentValidation;

namespace buddy.Features.Feelings;

public static class EditFeelingCheckInHandler
{
    public static async Task<EditFeelingCheckInOutcome> Handle(
        EditFeelingCheckIn command,
        IValidator<EditFeelingCheckIn> validator,
        IFeelingsJournalEventStore journals,
        IGuardianLinkEventStore guardians,
        IUserEventStore users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new EditFeelingCheckInOutcome.Validation(problem);
        }

        var access = await FeelingsAuthorization.CheckManage(command.ChildId, command.UserId, guardians, cancellationToken);

        if (access != FeelingsAccess.Allowed)
        {
            return EditFeelingCheckInOutcome.Denied(access);
        }

        if (await ChildLocalClock.NowAsync(command.ChildId, users, time, cancellationToken) is not { } now)
        {
            return new EditFeelingCheckInOutcome.NotFound();
        }

        var id = FeelingsJournalId.ForChild(command.ChildId);
        var journal = FeelingsJournal.Rehydrate(await journals.ReadAsync(id, cancellationToken));

        if (journal?.CheckIns.GetValueOrDefault(command.CheckInId) is not { } before)
        {
            return new EditFeelingCheckInOutcome.NotFound();
        }

        // The child's words stay the child's: a guardian may remove a self-report, never rewrite it.
        if (before.IsSelfReportOf(command.ChildId))
        {
            return new EditFeelingCheckInOutcome.IsSelfReport();
        }

        if (FeelingsMoments.IsInTheFuture(command.Date, command.Time, now))
        {
            return new EditFeelingCheckInOutcome.Validation(ValidationProblem.Of("A check-in can't describe a moment in the future."));
        }

        if (command.Date != before.LocalDate && journal.CountOn(command.Date) >= FeelingsRules.MaxCheckInsPerDay)
        {
            return new EditFeelingCheckInOutcome.Validation(ValidationProblem.Of($"At most {FeelingsRules.MaxCheckInsPerDay} check-ins a day."));
        }

        var after = before with
        {
            Zone = command.Zone,
            Tags = command.Tags,
            Note = command.Note,
            LocalDate = command.Date,
            LocalTime = command.Time
        };

        // An identical edit appends nothing, which keeps the PUT idempotent.
        if (before.HasSameContentAs(after))
        {
            return new EditFeelingCheckInOutcome.Success(FeelingCheckInResponse.From(before, command.ChildId));
        }

        await journals.AppendAsync(id, [new FeelingCheckInEdited(id, before, after, command.UserId, now.Instant)], cancellationToken);

        return new EditFeelingCheckInOutcome.Success(FeelingCheckInResponse.From(after, command.ChildId));
    }
}
