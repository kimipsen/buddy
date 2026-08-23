using buddy.Features.Mealplans;

namespace buddy.IntegrationTests.Features.Mealplans;

// Shared response shapes for the Mealplans endpoint tests, matching MealResponse /
// MealPlanEntry (Features/Mealplans/*). Strongly-typed ids serialize as a raw Guid
// (StronglyTypedIdJsonConverterFactory).
internal sealed record MealRatingDto(int Stars, string? Comment, DateTimeOffset RatedAt);

internal sealed record MealRatingEntryDto(Guid ChildId, int Stars, string? Comment, DateTimeOffset RatedAt);

internal sealed record MealDto(
    Guid Id,
    string Name,
    string? Description,
    string Icon,
    string Color,
    bool IsArchived,
    IReadOnlyList<MealRatingEntryDto> Ratings,
    Guid CreatedBy,
    Guid LastModifiedBy);

internal sealed record MealPlanEntryDto(
    DateOnly Date,
    MealSlot Slot,
    Guid MealId,
    string MealName,
    string Icon,
    string Color,
    MealRatingDto? Rating,
    string? Notes,
    Guid AssignedBy);
