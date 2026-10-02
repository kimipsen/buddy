using System.Text.Json.Serialization;

using buddy.Features.Calendars;

namespace buddy.Features.WorkLocations;

// Icon/Color reuse Calendars' value types -- the same one-way dependency Medicines and Mealplans
// already take on Calendars. Archived locations stay in the list so overrides and print templates
// that still reference them keep resolving (see work-locations.md, Question 3).
public sealed record WorkLocation(WorkLocationId Id, string Name, Icon Icon, Color Color, bool IsArchived = false)
{
    // Derived, so kept out of the persisted snapshot JSON.
    [JsonIgnore]
    public WorkLocationDetails Details => new(Name, Icon, Color);
}

public sealed record WorkLocationDetails(string Name, Icon Icon, Color Color);
