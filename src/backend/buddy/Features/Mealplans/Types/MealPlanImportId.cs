namespace buddy.Features.Mealplans;

public sealed record MealPlanImportId(Guid Value)
{
    public static MealPlanImportId New() => new(Guid.CreateVersion7());
}
