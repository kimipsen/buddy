namespace buddy.Features.Mealplans;

public sealed record MealplanAiSessionId(Guid Value)
{
    public static MealplanAiSessionId New() => new(Guid.CreateVersion7());
}
