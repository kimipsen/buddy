using System.Text.Json.Serialization;

namespace buddy.Features.Mealplans;

// Mealie's wire shapes, only the fields Buddy reads (verified against the v3.20 and v3.29 OpenAPI
// specs). External wire shapes stay nullable (docs/backend/analysis/eliminate-nulls.md); unknown
// fields are ignored, so a newer Mealie that adds fields still parses.
public sealed record MealieAboutDto(string? Version);

public sealed record MealieUserDto(
    Guid Id,
    string? Username,
    bool Admin,
    string? Household,
    Guid HouseholdId,
    Guid GroupId,
    string? GroupSlug);

public sealed record MealiePageDto<T>(
    int Page,
    [property: JsonPropertyName("per_page")] int PerPage,
    int Total,
    [property: JsonPropertyName("total_pages")] int TotalPages,
    IReadOnlyList<T>? Items);

public sealed record MealieRecipeSummaryDto(
    Guid Id,
    string? Slug,
    string? Name,
    string? Description,
    string? Image,
    string? TotalTime,
    string? RecipeYield);

public sealed record MealieRecipeDto(
    Guid Id,
    string? Slug,
    string? Name,
    string? Description,
    string? Image,
    string? TotalTime,
    string? RecipeYield,
    [property: JsonPropertyName("orgURL")] string? OrgUrl,
    IReadOnlyList<MealieIngredientDto>? RecipeIngredient,
    IReadOnlyList<MealieStepDto>? RecipeInstructions);

public sealed record MealieIngredientDto(string? Title, string? Display, string? Note);

public sealed record MealieStepDto(string? Title, string? Text);

public sealed record MealiePlanEntryDto(
    int Id,
    DateOnly Date,
    string? EntryType,
    string? Title,
    string? Text,
    Guid? RecipeId,
    MealieRecipeSummaryDto? Recipe);

public sealed record MealieShoppingListDto(Guid Id, string? Name);

public sealed record MealieShoppingListRecipeDto(Guid RecipeId, double RecipeIncrementQuantity);

public sealed record MealieImage(byte[] Content, string ContentType);
