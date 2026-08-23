namespace buddy.Features.Mealplans;

public sealed record MealId(Guid Value)
{
    public static MealId New() => new(Guid.CreateVersion7());
}
