namespace buddy.Features.WorkLocations;

public sealed record WorkLocationId(Guid Value)
{
    public static WorkLocationId New() => new(Guid.CreateVersion7());
}
