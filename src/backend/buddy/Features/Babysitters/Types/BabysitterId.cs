namespace buddy.Features.Babysitters;

public sealed record BabysitterId(Guid Value)
{
    public static BabysitterId New() => new(Guid.CreateVersion7());
}
