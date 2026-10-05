using buddy.Features.Pickups;

namespace buddy.IntegrationTests.Features.Pickups;

// Shared response shape for the Pickups endpoint tests, matching PickupOccurrence
// (Features/Pickups/Types/PickupOccurrence.cs). Strongly-typed ids serialize as a raw Guid
// (StronglyTypedIdJsonConverterFactory).
internal sealed record PickupOccurrenceDto(
    DateOnly Date,
    PickupSlot Slot,
    PickupAssigneeTestDto Assignee,
    TimeOnly? Time,
    string Notes,
    Guid AssignedBy);

// PickupAssigneeDto read flat: "kind" plus whichever case fields the response carries.
internal sealed record PickupAssigneeTestDto(
    PickupAssigneeKind Kind,
    Guid? GuardianId = null,
    Guid? SiblingChildId = null,
    string? HostName = null,
    string? Location = null,
    string? ContactInfo = null,
    Guid? BabysitterId = null,
    string? Name = null);
