using System.Collections.Immutable;

using FluentValidation;

namespace buddy.Features.Progress;

public sealed class ConfigureGoalPostsValidator : AbstractValidator<ConfigureGoalPosts>
{
    public ConfigureGoalPostsValidator()
    {
        RuleFor(x => x.GoalPosts)
            .NotEmpty().WithMessage("At least one goal post is required.")
            .Must(posts => posts.Length <= 20).WithMessage("At most 20 goal posts are allowed.")
            .Must(BeStrictlyAscending).WithMessage("Goal posts must have strictly increasing thresholds.");

        RuleForEach(x => x.GoalPosts).ChildRules(post =>
        {
            post.RuleFor(p => p.Threshold).GreaterThan(0);
            post.RuleFor(p => p.Icon).NotEmpty();
        });
    }

    private static bool BeStrictlyAscending(ImmutableArray<GoalPost> posts)
    {
        for (var i = 1; i < posts.Length; i++)
        {
            if (posts[i].Threshold <= posts[i - 1].Threshold)
            {
                return false;
            }
        }

        return true;
    }
}
