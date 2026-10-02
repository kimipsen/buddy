namespace buddy.Features.WorkLocations;

// A guardian's exception to their pattern for one date: at a specific location, or not at any
// location (day off, holiday, sick). Having no override at all means "follow the pattern".
// Persisted in the events and the snapshot through WorkDayOverrideJsonConverter. See
// docs/backend/analysis/eliminate-nulls.md, Phase 5.7.
public union WorkDayOverride(WorkDayOverride.AtLocation, WorkDayOverride.DayOff)
{
    public sealed record AtLocation(WorkLocationId LocationId);

    public sealed record DayOff;
}
