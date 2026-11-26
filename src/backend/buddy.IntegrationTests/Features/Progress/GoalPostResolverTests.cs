using System.Collections.Immutable;

using buddy.Features.Progress;

using Xunit;

namespace buddy.IntegrationTests.Features.Progress;

public sealed class GoalPostResolverTests
{
    private static readonly ImmutableArray<GoalPost> Posts = [new GoalPost(5, "a", ""), new GoalPost(10, "b", ""), new GoalPost(20, "c", "")];

    private static int[] Crossed(int from, int to, ImmutableArray<GoalPost>? posts = null) =>
        [.. GoalPostResolver.CrossedBetween(posts ?? Posts, from, to).Select(p => p.Threshold)];

    [Fact]
    public void The_lower_bound_is_exclusive_and_the_upper_bound_inclusive()
    {
        Assert.Equal([10], Crossed(5, 10));
        Assert.Empty(Crossed(5, 9));
        Assert.Equal([5], Crossed(4, 5));
        Assert.Empty(Crossed(5, 5));
    }

    [Fact]
    public void Several_thresholds_crossed_by_one_award_come_back_ascending()
    {
        Assert.Equal([5, 10, 20], Crossed(0, 25));
        Assert.Equal([10, 20], Crossed(7, 20));
    }

    [Fact]
    public void Nothing_is_crossed_when_the_total_does_not_move_or_goes_down()
    {
        Assert.Empty(Crossed(12, 12));
        Assert.Empty(Crossed(12, 3));
    }

    [Fact]
    public void An_unconfigured_child_uses_the_default_scale()
    {
        Assert.Equal([5, 10, 25], Crossed(0, 25, ImmutableArray<GoalPost>.Empty));
    }

    [Fact]
    public void Thresholds_past_the_last_configured_one_are_extrapolated()
    {
        // Step is 10 (20 - 10): 30, 40 follow the configured 5, 10, 20.
        Assert.Equal([20, 30, 40], Crossed(15, 40));
    }

    [Fact]
    public void A_single_goal_post_extrapolates_by_its_own_threshold()
    {
        Assert.Equal([3, 6, 9], Crossed(0, 9, [new GoalPost(3, "a", "")]));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(30)]
    public void It_agrees_with_AtThreshold_for_a_single_star_step(int stars)
    {
        var atThreshold = GoalPostResolver.AtThreshold(Posts, stars);
        var crossed = GoalPostResolver.CrossedBetween(Posts, stars - 1, stars);

        Assert.Equal(atThreshold is null ? [] : [atThreshold.Threshold], crossed.Select(p => p.Threshold));
    }

    [Fact]
    public void A_threshold_configured_after_the_stars_were_earned_is_crossed_by_the_next_award_that_spans_it()
    {
        // 12 stars earned, a goal post at 8 is configured later: only an award that spans 8 reports it,
        // so no award from 12 ever does (the handler never re-emits what lies behind the total).
        ImmutableArray<GoalPost> late = [new GoalPost(8, "x", ""), new GoalPost(15, "y", "")];

        Assert.Empty(Crossed(12, 13, late));
        Assert.Equal([15], Crossed(12, 15, late));
    }
}
