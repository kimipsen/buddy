using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Babysitters;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.NightDuty;

public static class SetNightOverridesHandler
{
    public static async Task<Result<IReadOnlyCollection<Night>>> Handle(
        SetNightOverrides command,
        IValidator<SetNightOverrides> validator,
        INightScheduleEventStore store,
        IGuardianLinkEventStore guardians,
        IBabysitterListEventStore babysitters,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<IReadOnlyCollection<Night>>.Validation(problem);
        }

        var access = await NightDutyAuthorization.CheckManage(command.ChildId, command.UserId, guardians, cancellationToken);

        if (access != NightDutyAccess.Allowed)
        {
            return access.ToDeniedResult<IReadOnlyCollection<Night>>();
        }

        var now = time.GetUtcNow();
        var loaded = await NightScheduleWriter.LoadAsync(store, command.ChildId, cancellationToken);
        var schedule = loaded.Schedule;
        var plannedEntries = command.Nights.Select(n => n.Override.PlannedEntry).OfType<NightEntry>();

        // People already on the nights being set -- their current override or scheduled entry -- aren't
        // checked again, so a switch can move a babysitter who has since been archived.
        var current = command.Nights
            .SelectMany(n => new[]
            {
                schedule.Overrides.TryGetValue(n.Date, out var existing) ? existing.PlannedEntry : null,
                schedule.ScheduledEntryFor(n.Date),
            })
            .OfType<NightEntry>();

        if (await NightDutyRules.CheckRelationshipsAsync(command.ChildId, plannedEntries, current, guardians, babysitters, cancellationToken) is { } relationshipProblem)
        {
            return new Result<IReadOnlyCollection<Night>>.Validation(relationshipProblem);
        }

        // One event per date that actually changes, in date order; dates already holding this
        // override emit nothing.
        List<NightDutyEvent> events = [.. command.Nights
            .OrderBy(n => n.Date)
            .Where(n => !schedule.Overrides.TryGetValue(n.Date, out var existing) || !existing.Equals(n.Override))
            .Select(n => (NightDutyEvent)new NightOverridden(schedule.Id, n.Date, n.Override, command.UserId, now))];

        schedule = await NightScheduleWriter.SaveAsync(store, loaded, events, now, cancellationToken);

        var dates = command.Nights.Select(n => n.Date).ToHashSet();
        var nights = await NightExpansion.ExpandAsync(schedule, dates.Min(), dates.Max(), babysitters, cancellationToken);

        return new Result<IReadOnlyCollection<Night>>.Success([.. nights.Where(n => dates.Contains(n.Date))]);
    }
}
