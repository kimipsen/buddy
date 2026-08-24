using buddy.Features.Medicines;

namespace buddy.IntegrationTests.Features.Medicines;

// Shared response shapes for the Medicines endpoint tests, matching MedicineScheduleResponse /
// MedicineDoseOccurrence (Features/Medicines/*). Strongly-typed ids serialize as a raw Guid
// (StronglyTypedIdJsonConverterFactory).
internal sealed record MedicineScheduleDto(
    Guid Id,
    Guid ChildId,
    string Name,
    string Dosage,
    string Icon,
    string Color,
    IReadOnlyList<TimeOnly> Times,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool IsStopped,
    Guid CreatedBy,
    Guid LastModifiedBy);

internal sealed record MedicineDoseOccurrenceDto(
    Guid MedicineId,
    string Name,
    string Dosage,
    string Icon,
    string Color,
    DateOnly Date,
    TimeOnly Time,
    DoseStatus Status);

internal sealed record SharedMedicineGroupResponseDto(Guid? GroupId, string? GroupName);
