namespace buddy.Features.Mealplans;

public sealed record MealieConnectionId(Guid Value)
{
    public static MealieConnectionId New() => new(Guid.CreateVersion7());
}
