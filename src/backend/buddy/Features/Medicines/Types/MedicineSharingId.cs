namespace buddy.Features.Medicines;

public sealed record MedicineSharingId(Guid Value)
{
    public static MedicineSharingId New() => new(Guid.CreateVersion7());
}
