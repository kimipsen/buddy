namespace buddy.IntegrationTests.Features.TaskLibrary;

// Shared response shapes for the TaskLibrary endpoint tests, matching TaskTemplateResponse /
// SubtaskResponse (Features/TaskLibrary/*). Strongly-typed ids serialize as a raw Guid
// (StronglyTypedIdJsonConverterFactory) -- same contract as MealplanTestDtos.cs. Enums go over the
// wire by name, so Kind and Status are read as strings.
internal sealed record SubtaskDto(Guid Id, string Title, string? Icon, TimeSpan Duration, IReadOnlyList<DayOfWeek> Weekdays);

internal sealed record ChecklistOptionsDto(TimeOnly? EveningBeforeFrom);

internal sealed record ChecklistSuggestionDto(
    Guid Id,
    string Title,
    IReadOnlyList<DayOfWeek> Weekdays,
    Guid SuggestedBy,
    string Status,
    DateTimeOffset SuggestedAt,
    DateTimeOffset? ResolvedAt,
    Guid? SubtaskId);

internal sealed record TaskTemplateDto(
    Guid Id,
    string Name,
    string Icon,
    string Color,
    IReadOnlyList<SubtaskDto> Subtasks,
    TimeSpan TotalDuration,
    bool IsArchived,
    Guid CreatedBy,
    Guid LastModifiedBy,
    string Kind,
    ChecklistOptionsDto? Checklist,
    IReadOnlyList<ChecklistSuggestionDto> Suggestions);
