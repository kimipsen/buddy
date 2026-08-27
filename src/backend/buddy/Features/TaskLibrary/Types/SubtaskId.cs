namespace buddy.Features.TaskLibrary;

public sealed record SubtaskId(Guid Value)
{
    public static SubtaskId New() => new(Guid.CreateVersion7());
}
