using FluentValidation;

namespace buddy.Features.Progress;

public sealed class ConfigureRewardsValidator : AbstractValidator<ConfigureRewards>
{
    public const int MaxRewards = 30;
    public const int MaxCost = 10_000;

    public ConfigureRewardsValidator()
    {
        // An empty catalog is allowed: it's how a guardian removes every reward.
        RuleFor(x => x.Rewards)
            .Must(rewards => rewards.Length <= MaxRewards).WithMessage($"At most {MaxRewards} rewards are allowed.")
            .Must(rewards => rewards.Where(r => r.Id is not null).Select(r => r.Id).Distinct().Count() == rewards.Count(r => r.Id is not null))
            .WithMessage("Each reward id may appear only once.");

        RuleForEach(x => x.Rewards).ChildRules(reward =>
        {
            reward.RuleFor(r => r.Name).NotEmpty().MaximumLength(100);
            reward.RuleFor(r => r.Icon).NotEmpty().MaximumLength(32);
            reward.RuleFor(r => r.Cost).InclusiveBetween(1, MaxCost);
        });
    }
}
