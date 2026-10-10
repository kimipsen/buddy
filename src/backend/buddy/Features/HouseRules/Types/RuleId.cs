namespace buddy.Features.HouseRules;

public sealed record RuleId(Guid Value)
{
    public static RuleId New() => new(Guid.CreateVersion7());
}
