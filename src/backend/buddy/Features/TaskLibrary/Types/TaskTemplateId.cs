namespace buddy.Features.TaskLibrary;

public sealed record TaskTemplateId(Guid Value)
{
    public static TaskTemplateId New() => new(Guid.CreateVersion7());
}
