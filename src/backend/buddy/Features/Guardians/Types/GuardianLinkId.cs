namespace buddy.Features.Guardians;

public sealed record GuardianLinkId(Guid Value)
{
    public static GuardianLinkId New() => new(Guid.CreateVersion7());
}
