using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Babysitters;
using buddy.Features.Guardians;
using buddy.Features.Users;

using FluentValidation;

namespace buddy.Features.NightDuty;

public static class ReplaceNightPatternHandler
{
    public static async Task<Result<NightScheduleResponse>> Handle(
        ReplaceNightPattern command,
        IValidator<ReplaceNightPattern> validator,
        INightScheduleEventStore store,
        IGuardianLinkEventStore guardians,
        IBabysitterListEventStore babysitters,
        IUserEventStore users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<NightScheduleResponse>.Validation(problem);
        }

        var access = await NightDutyAuthorization.CheckManage(command.ChildId, command.UserId, guardians, cancellationToken);

        if (access != NightDutyAccess.Allowed)
        {
            return access.ToDeniedResult<NightScheduleResponse>();
        }

        if (await UserLocalDate.TodayAsync(command.ChildId, users, time, cancellationToken) is not { } today)
        {
            return new Result<NightScheduleResponse>.NotFound();
        }

        // Past nights are frozen: a schedule save never reaches back before the child's today
        // (docs/backend/analysis/night-duty.md, Question 4).
        var effectiveFrom = command.EffectiveFrom ?? today;

        if (effectiveFrom < today)
        {
            return new Result<NightScheduleResponse>.Validation(ValidationProblem.Of("effectiveFrom can't be in the past."));
        }

        if (await NightDutyRules.CheckRelationshipsAsync(
                command.ChildId, command.Days.Select(d => d.Entry), guardians, babysitters, cancellationToken) is { } relationshipProblem)
        {
            return new Result<NightScheduleResponse>.Validation(relationshipProblem);
        }

        var now = time.GetUtcNow();
        var loaded = await NightScheduleWriter.LoadAsync(store, command.ChildId, cancellationToken);
        var schedule = loaded.Schedule;
        var pattern = new NightPattern(effectiveFrom, command.CycleWeeks, command.AnchorMonday, command.Days).Normalized();

        // A no-op only when this exact version is already the last one: saving it again would
        // neither change it nor drop a later version.
        var unchanged = schedule.Patterns.Count > 0 && schedule.Patterns[^1].IsSameAs(pattern);

        if (!unchanged)
        {
            schedule = await NightScheduleWriter.SaveAsync(
                store, loaded, [new NightPatternReplaced(schedule.Id, pattern, command.UserId, now)], now, cancellationToken);
        }

        var names = await BabysitterNames.LoadAsync(
            schedule.Patterns.SelectMany(p => p.Days.Select(d => d.Entry)).BabysitterOwners(), babysitters, cancellationToken);

        return new Result<NightScheduleResponse>.Success(NightScheduleResponse.From(schedule, today, names));
    }
}
