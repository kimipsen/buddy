namespace buddy.Features.WorkLocations;

public enum WorkDaySource
{
    // No override and no pattern entry for this date.
    None,
    Pattern,
    Override
}

// One resolved day for ListWorkDays. Location details are inlined so the print sheet needs exactly
// one call per guardian. Location is null for Source == None, and for an override to "off".
public sealed record WorkDay(DateOnly Date, WorkLocationSummary? Location, WorkDaySource Source);

public sealed record WorkLocationSummary(Guid Id, string Name, string Icon, string Color, bool IsArchived)
{
    public static WorkLocationSummary From(WorkLocation location) =>
        new(location.Id.Value, location.Name, location.Icon.Value, location.Color.Value, location.IsArchived);
}
