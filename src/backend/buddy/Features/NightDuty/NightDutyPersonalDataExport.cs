using buddy.Common.Erasure;
using buddy.Features.Babysitters;

namespace buddy.Features.NightDuty;

// The "nightDuty" section: each child's fixed-schedule versions and per-night overrides.
public sealed class NightDutyPersonalDataExporter(INightScheduleEventStore schedules, IBabysitterListEventStore babysitters) : IPersonalDataExporter
{
    public Type Store => typeof(INightDutyStore);

    public string Section => "nightDuty";

    public async Task<object?> ExportAsync(ExportSubject subject, CancellationToken cancellationToken)
    {
        List<ExportedNightSchedule> children = [];

        foreach (var childId in subject.Children)
        {
            if (await schedules.FindSnapshotAsync(NightScheduleId.ForChild(childId), cancellationToken) is not { } schedule)
            {
                continue;
            }

            var entries = schedule.Patterns.SelectMany(p => p.Days.Select(d => d.Entry))
                .Concat(schedule.Overrides.Values.Select(o => o.PlannedEntry).OfType<NightEntry>());
            var names = await BabysitterNames.LoadAsync(entries.BabysitterOwners(), babysitters, cancellationToken);

            children.Add(new ExportedNightSchedule(
                childId.Value,
                [.. schedule.Patterns.Select(p => new ExportedNightPattern(
                    p.EffectiveFrom,
                    p.CycleWeeks,
                    p.AnchorMonday,
                    [.. p.Days.Select(d => new NightPatternDayDto(d.Week, d.Day, NightEntryDto.FromDomain(d.Entry, names)))]))],
                [.. schedule.Overrides.OrderBy(o => o.Key).Select(o => new ExportedNightOverride(
                    o.Key,
                    o.Value.PlannedEntry is { } entry ? NightEntryDto.FromDomain(entry, names) : null))]));
        }

        return children;
    }
}

public sealed record ExportedNightSchedule(Guid ChildId, IReadOnlyList<ExportedNightPattern> Patterns, IReadOnlyList<ExportedNightOverride> Overrides);

public sealed record ExportedNightPattern(DateOnly EffectiveFrom, int CycleWeeks, DateOnly AnchorMonday, IReadOnlyList<NightPatternDayDto> Days);

// Entry null = the night was cleared ("nothing planned").
public sealed record ExportedNightOverride(DateOnly Date, NightEntryDto? Entry);
