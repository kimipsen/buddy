namespace buddy.Features.WorkLocations;

// HTTP shapes shared by several slices. Overrides are deliberately not included -- they're read
// resolved, through ListWorkDays.
public sealed record WorkLocationScheduleResponse(Guid GuardianId, IReadOnlyList<WorkLocationSummary> Locations, WorkPatternResponse Pattern)
{
    public static WorkLocationScheduleResponse From(WorkLocationSchedule schedule) =>
        new(schedule.GuardianId.Value, [.. schedule.Locations.Select(WorkLocationSummary.From)], WorkPatternResponse.From(schedule.Pattern));
}

public sealed record WorkPatternResponse(int CycleWeeks, DateOnly AnchorMonday, IReadOnlyList<WorkPatternDayDto> Days)
{
    public static WorkPatternResponse From(WorkPattern pattern) =>
        new(pattern.CycleWeeks, pattern.AnchorMonday, [.. pattern.Days.Select(d => new WorkPatternDayDto(d.Week, d.Day, d.LocationId.Value))]);
}

public sealed record WorkPatternDayDto(int Week, DayOfWeek Day, Guid LocationId);
