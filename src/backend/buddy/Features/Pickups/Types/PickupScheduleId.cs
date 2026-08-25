namespace buddy.Features.Pickups;

public sealed record PickupScheduleId(Guid Value)
{
    public static PickupScheduleId New() => new(Guid.CreateVersion7());
}
