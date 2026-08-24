namespace buddy.Features.Guardians;

public sealed record GuardianInviteId(Guid Value)
{
    public static GuardianInviteId New() => new(Guid.CreateVersion7());
}
