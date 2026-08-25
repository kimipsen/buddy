using buddy.Features.Pickups;

namespace buddy.IntegrationTests.Features.Pickups;

// Shared response shape for the Pickups endpoint tests, matching PickupOccurrence
// (Features/Pickups/Types/PickupOccurrence.cs). Strongly-typed ids serialize as a raw Guid
// (StronglyTypedIdJsonConverterFactory).
internal sealed record PickupOccurrenceDto(
    DateOnly Date,
    PickupSlot Slot,
    PickupAssigneeKind Kind,
    Guid? GuardianId,
    Guid? SiblingChildId,
    string? PlaydateHostName,
    string? PlaydateLocation,
    string? PlaydateContactInfo,
    TimeOnly? Time,
    string? Notes,
    Guid AssignedBy);
