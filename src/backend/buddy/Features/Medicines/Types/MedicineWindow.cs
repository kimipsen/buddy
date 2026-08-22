namespace buddy.Features.Medicines;

public sealed record MedicineWindow(IReadOnlyList<TimeOnly> Times, DateOnly StartDate, DateOnly? EndDate);
