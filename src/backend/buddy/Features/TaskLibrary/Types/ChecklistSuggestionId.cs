namespace buddy.Features.TaskLibrary;

public sealed record ChecklistSuggestionId(Guid Value)
{
    public static ChecklistSuggestionId New() => new(Guid.CreateVersion7());
}
