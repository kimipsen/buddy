using buddy.Common.Erasure;

namespace buddy.Features.WorkLocations;

// The "workLocations" section: the caller's locations, weekly pattern and per-day overrides.
public sealed class WorkLocationsPersonalDataExporter(IWorkLocationScheduleEventStore schedules) : IPersonalDataExporter
{
    public Type Store => typeof(IWorkLocationsStore);

    public string Section => "workLocations";

    public async Task<object?> ExportAsync(ExportSubject subject, CancellationToken cancellationToken)
    {
        if (await schedules.FindSnapshotAsync(WorkLocationScheduleId.ForGuardian(subject.UserId), cancellationToken) is not { } schedule)
        {
            return null;
        }

        return new WorkLocationsExport(
            WorkLocationScheduleResponse.From(schedule),
            [.. schedule.Overrides.OrderBy(o => o.Key).Select(o => o.Value switch
            {
                WorkDayOverride.AtLocation at => new ExportedWorkDayOverride(o.Key, at.LocationId.Value, DayOff: false),
                _ => new ExportedWorkDayOverride(o.Key, LocationId: null, DayOff: true),
            })]);
    }
}

public sealed record WorkLocationsExport(WorkLocationScheduleResponse Schedule, IReadOnlyList<ExportedWorkDayOverride> Overrides);

public sealed record ExportedWorkDayOverride(DateOnly Date, Guid? LocationId, bool DayOff);
