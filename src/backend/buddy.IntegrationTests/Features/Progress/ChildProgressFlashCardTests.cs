using System.Collections.Immutable;

using buddy.Features.Progress;
using buddy.Features.Users;

using Xunit;

namespace buddy.IntegrationTests.Features.Progress;

// The ChildProgress fold for FlashCardStarsAwarded: cap accumulation, pruning and the old-snapshot default.
public sealed class ChildProgressFlashCardTests
{
    private static readonly UserId Child = new(Guid.Parse("00000000-0000-0000-0000-000000000003"));
    private static readonly ProgressId Id = ProgressId.ForChild(Child);
    private static readonly Guid SetA = Guid.Parse("00000000-0000-0000-0000-0000000000a0");
    private static readonly Guid SetB = Guid.Parse("00000000-0000-0000-0000-0000000000b0");
    private static readonly DateTimeOffset Now = new(2025, 6, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Day = new(2025, 6, 10);

    private static ChildProgress Award(ChildProgress progress, Guid set, DateOnly day, int stars) =>
        ChildProgress.Advance(progress, new FlashCardStarsAwarded(Id, set, day, stars, Guid.NewGuid(), Now));

    private static ChildProgress Fresh() => ChildProgress.Initial(Id, Child);

    [Fact]
    public void An_award_raises_the_total_and_the_cap_entry_of_its_set_and_day()
    {
        var progress = Award(Award(Fresh(), SetA, Day, 3), SetA, Day, 2);

        Assert.Equal(5, progress.TotalStars);
        Assert.Equal<FlashCardDayStars>([new FlashCardDayStars(SetA, Day, 5)], progress.FlashCardStarsByDay.AsEnumerable());
        Assert.Equal(5, progress.FlashCardStarsOn(SetA, Day));
        Assert.Equal(0, progress.FlashCardStarsOn(SetB, Day));
        Assert.Equal(0, progress.FlashCardStarsOn(SetA, Day.AddDays(1)));
    }

    [Fact]
    public void Different_sets_and_days_keep_separate_entries()
    {
        var progress = Award(Award(Award(Fresh(), SetA, Day, 3), SetB, Day, 4), SetA, Day.AddDays(1), 2);

        Assert.Equal(9, progress.TotalStars);
        Assert.Equal(3, progress.FlashCardStarsOn(SetA, Day));
        Assert.Equal(4, progress.FlashCardStarsOn(SetB, Day));
        Assert.Equal(2, progress.FlashCardStarsOn(SetA, Day.AddDays(1)));
    }

    [Fact]
    public void Entries_more_than_seven_days_older_than_the_newest_day_are_pruned_and_the_total_is_kept()
    {
        var progress = Award(Fresh(), SetA, Day, 3);
        progress = Award(progress, SetA, Day.AddDays(7), 1);

        // Exactly 7 days older than the newest: kept.
        Assert.Equal(2, progress.FlashCardStarsByDay.Length);

        progress = Award(progress, SetA, Day.AddDays(8), 1);

        // Now Day is 8 days behind the newest: pruned. TotalStars is untouched.
        Assert.Equal<FlashCardDayStars>([new FlashCardDayStars(SetA, Day.AddDays(7), 1), new FlashCardDayStars(SetA, Day.AddDays(8), 1)], progress.FlashCardStarsByDay.AsEnumerable());
        Assert.Equal(5, progress.TotalStars);
    }

    [Fact]
    public void A_late_award_within_seven_days_is_kept_and_one_beyond_is_pruned_at_once_but_still_counts()
    {
        var progress = Award(Fresh(), SetA, Day, 3);

        var within = Award(progress, SetA, Day.AddDays(-7), 2);
        Assert.Equal(5, within.TotalStars);
        Assert.Equal(2, within.FlashCardStarsOn(SetA, Day.AddDays(-7)));

        var beyond = Award(progress, SetA, Day.AddDays(-8), 2);
        Assert.Equal(5, beyond.TotalStars);
        Assert.Equal(0, beyond.FlashCardStarsOn(SetA, Day.AddDays(-8)));
        Assert.Equal<FlashCardDayStars>([new FlashCardDayStars(SetA, Day, 3)], beyond.FlashCardStarsByDay.AsEnumerable());
    }

    [Fact]
    public void Newest_means_the_largest_stored_day_not_the_day_of_the_latest_event()
    {
        var progress = Award(Fresh(), SetA, Day, 1);
        progress = Award(progress, SetA, Day.AddDays(-3), 1);
        progress = Award(progress, SetB, Day.AddDays(-5), 1);

        Assert.Equal(3, progress.FlashCardStarsByDay.Length);
    }

    [Fact]
    public void A_snapshot_without_the_member_reads_as_an_empty_array()
    {
        // What System.Text.Json does for a snapshot with no such member: the constructor gets default.
        var fresh = Fresh();
        var old = new ChildProgress(
            fresh.Id, fresh.ChildId, 0, fresh.AwardedOccurrences, fresh.UnlockedMilestones, fresh.GoalPosts,
            fresh.Rewards, 0, fresh.RewardRequests, default(ImmutableArray<FlashCardDayStars>));

        Assert.Empty(old.FlashCardStarsByDay);
        Assert.Equal(0, old.FlashCardStarsOn(SetA, Day));
        Assert.Equal(2, Award(old, SetA, Day, 2).TotalStars);
    }

    [Fact]
    public void Initial_has_no_cap_entries()
    {
        Assert.Empty(Fresh().FlashCardStarsByDay);
    }
}
