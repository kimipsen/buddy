namespace buddy.Features.Medicines;

public sealed record MedicineId(Guid Value)
{
    public static MedicineId New() => new(Guid.CreateVersion7());
}
