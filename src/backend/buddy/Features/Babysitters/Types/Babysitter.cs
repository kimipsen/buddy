using System.Text.Json.Serialization;

namespace buddy.Features.Babysitters;

// ContactInfo is free text like PickupAssignee.Playdate's, "" meaning none. Archived babysitters stay
// in the list so pickup slots that still reference them keep resolving (see babysitters.md, Question 3).
public sealed record Babysitter(BabysitterId Id, string Name, string ContactInfo, bool IsArchived = false)
{
    // Derived, so kept out of the persisted snapshot JSON.
    [JsonIgnore]
    public BabysitterDetails Details => new(Name, ContactInfo);
}

public sealed record BabysitterDetails(string Name, string ContactInfo);

public sealed record BabysitterSummary(Guid Id, string Name, string ContactInfo, bool IsArchived)
{
    public static BabysitterSummary From(Babysitter babysitter) =>
        new(babysitter.Id.Value, babysitter.Name, babysitter.ContactInfo, babysitter.IsArchived);
}
