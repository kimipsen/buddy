namespace buddy.Features.WorkLocations;

// A null LocationId means "not at any location that day" (day off, holiday, sick) -- which is
// different from having no override at all, which means "follow the pattern".
public sealed record WorkDayOverride(WorkLocationId? LocationId);
