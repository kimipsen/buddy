namespace buddy.Features.Users;

public sealed record UserId(Guid Value)
{
    public static UserId New() => new(Guid.CreateVersion7());
}