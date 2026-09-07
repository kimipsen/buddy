namespace buddy.Features.Mealplans;

public sealed record AiCredentialId(Guid Value)
{
    public static AiCredentialId New() => new(Guid.CreateVersion7());
}
