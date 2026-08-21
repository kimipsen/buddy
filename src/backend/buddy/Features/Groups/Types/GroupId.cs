namespace buddy.Features.Groups;

public sealed record GroupId(Guid Value)
{
    public static GroupId New() => new(Guid.CreateVersion7());
}
