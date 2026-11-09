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

        var now = time.GetUtcNow();
        var loaded = await NightScheduleWriter.LoadAsync(store, command.ChildId, cancellationToken);
        var schedule = loaded.Schedule;

        // People already on the versions this save replaces (the one in effect on effectiveFrom and
        // any later one) aren't checked again.
        var replaced = schedule.Patterns
            .Where(p => p.EffectiveFrom >= effectiveFrom || p == schedule.PatternFor(effectiveFrom))
            .SelectMany(p => p.Days.Select(d => d.Entry));

        if (await NightDutyRules.CheckRelationshipsAsync(
                command.ChildId, command.Days.Select(d => d.Entry), replaced, guardians, babysitters, cancellationToken) is { } relationshipProblem)
        {
            return new Result<NightScheduleResponse>.Validation(relationshipProblem);
        }

        var pattern = new NightPattern(effectiveFrom, command.CycleWeeks, command.AnchorMonday, command.Days).Normalized();

        // A no-op when the version in effect on that date is the last one and already says the same:
        // saving it again would neither change a night nor drop a later version.
        var inEffect = schedule.PatternFor(effectiveFrom);
        var unchanged = inEffect is not null
            && ReferenceEquals(inEffect, schedule.Patterns[^1])
            && inEffect.IsSameAs(pattern with { EffectiveFrom = inEffect.EffectiveFrom });

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
