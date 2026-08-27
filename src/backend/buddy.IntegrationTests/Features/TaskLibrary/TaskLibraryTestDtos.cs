namespace buddy.IntegrationTests.Features.TaskLibrary;

// Shared response shapes for the TaskLibrary endpoint tests, matching TaskTemplateResponse /
// SubtaskResponse (Features/TaskLibrary/*). Strongly-typed ids serialize as a raw Guid
// (StronglyTypedIdJsonConverterFactory) -- same contract as MealplanTestDtos.cs.
internal sealed record SubtaskDto(Guid Id, string Title, string? Icon, TimeSpan Duration);

internal sealed record TaskTemplateDto(
    Guid Id,
    string Name,
    string Icon,
    string Color,
    IReadOnlyList<SubtaskDto> Subtasks,
    TimeSpan TotalDuration,
    bool IsArchived,
    Guid CreatedBy,
    Guid LastModifiedBy);
