namespace buddy.Features.Mealplans;

public sealed record MealPlanId(Guid Value)
{
    public static MealPlanId New() => new(Guid.CreateVersion7());
}
