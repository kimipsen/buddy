using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;
using buddy.Features.Users;

using FluentValidation;

namespace buddy.Features.Feelings;

public static class CheckInHandler
{
    public static async Task<Result<FeelingCheckInResponse>> Handle(
        CheckIn command,
        IValidator<CheckIn> validator,
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

        var access = await FeelingsAuthorization.CheckSelf(command.UserId, guardians, cancellationToken);

        if (access != FeelingsAccess.Allowed)
        {
            return access.ToDeniedResult<FeelingCheckInResponse>();
        }

        if (await ChildLocalClock.NowAsync(command.UserId, users, time, cancellationToken) is not { } now)
        {
            return new Result<FeelingCheckInResponse>.NotFound();
        }

        var id = FeelingsJournalId.ForChild(command.UserId);
        var events = await journals.ReadAsync(id, cancellationToken);
        var journal = events.Count == 0 ? null : FeelingsJournal.Replay(events);

        if (journal is not null && journal.CountOn(now.Date) >= FeelingsRules.MaxCheckInsPerDay)
        {
            return new Result<FeelingCheckInResponse>.Validation(
                ValidationProblem.Of($"At most {FeelingsRules.MaxCheckInsPerDay} check-ins a day."));
        }

        var checkIn = new FeelingCheckIn(
            FeelingCheckInId.New(), command.Zone, command.Tags, command.Note, now.Date, now.Time, command.UserId, now.Instant);

        await FeelingsJournalWrites.WriteAsync(
            journals,
            id,
            journal,
            new FeelingsJournalStarted(id, command.UserId, now.Instant),
            new FeelingCheckedIn(id, checkIn, now.Instant),
            cancellationToken);

        return new Result<FeelingCheckInResponse>.Success(FeelingCheckInResponse.From(checkIn, command.UserId));
    }
}
