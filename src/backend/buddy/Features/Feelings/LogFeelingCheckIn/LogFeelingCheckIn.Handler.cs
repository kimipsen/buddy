using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;
using buddy.Features.Users;

using FluentValidation;

namespace buddy.Features.Feelings;

public static class LogFeelingCheckInHandler
{
    public static async Task<Result<FeelingCheckInResponse>> Handle(
        LogFeelingCheckIn command,
        IValidator<LogFeelingCheckIn> validator,
        IFeelingsJournalEventStore journals,
        IGuardianLinkEventStore guardians,
        IUserEventStore users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<FeelingCheckInResponse>.Validation(problem);
        }

        var access = await FeelingsAuthorization.CheckManage(command.ChildId, command.UserId, guardians, cancellationToken);

        if (access != FeelingsAccess.Allowed)
        {
            return access.ToDeniedResult<FeelingCheckInResponse>();
        }

        if (await ChildLocalClock.NowAsync(command.ChildId, users, time, cancellationToken) is not { } now)
        {
            return new Result<FeelingCheckInResponse>.NotFound();
        }

        var date = command.Date ?? now.Date;
        var localTime = command.Time ?? now.Time;

        if (FeelingsMoments.IsInTheFuture(date, localTime, now))
        {
            return new Result<FeelingCheckInResponse>.Validation(ValidationProblem.Of("A check-in can't describe a moment in the future."));
        }

        var id = FeelingsJournalId.ForChild(command.ChildId);
        var events = await journals.ReadAsync(id, cancellationToken);
        var journal = events.Count == 0 ? null : FeelingsJournal.Replay(events);

        if (journal is not null && journal.CountOn(date) >= FeelingsRules.MaxCheckInsPerDay)
        {
            return new Result<FeelingCheckInResponse>.Validation(
                ValidationProblem.Of($"At most {FeelingsRules.MaxCheckInsPerDay} check-ins a day."));
        }

        var checkIn = new FeelingCheckIn(
            FeelingCheckInId.New(), command.Zone, command.Tags, command.Note, date, localTime, command.UserId, now.Instant);

        await FeelingsJournalWrites.WriteAsync(
            journals,
            id,
            journal,
            new FeelingsJournalStarted(id, command.ChildId, now.Instant),
            new FeelingCheckedIn(id, checkIn, now.Instant),
            cancellationToken);

        return new Result<FeelingCheckInResponse>.Success(FeelingCheckInResponse.From(checkIn, command.ChildId));
    }
}

internal static class FeelingsMoments
{
    // A guardian may backdate without limit, but not post-date beyond a few minutes of clock slack.
    public static bool IsInTheFuture(DateOnly date, TimeOnly time, ChildLocalNow now) =>
        date.ToDateTime(time) > now.Date.ToDateTime(now.Time).Add(FeelingsRules.FutureSlack);
}
