using buddy.Common.Concurrency;
using buddy.Features.Progress;
using buddy.Features.Users;
using buddy.IntegrationTests.Common.Concurrency;
using buddy.IntegrationTests.Fixtures;

using JasperFx;

using Microsoft.Extensions.DependencyInjection;

using Wolverine;

using Xunit;

namespace buddy.IntegrationTests.Features.Progress.AwardFlashCardStars;

// AwardFlashCardStars has no endpoint (the FlashCards finish handler, a later slice, calls it through
// the bus), so these invoke it through the real Wolverine pipeline -- the version-scope middleware
// included -- against real Marten. The 409 mapping itself (ConcurrencyConflictMiddleware) is covered
// by OptimisticConcurrencyTests; here a lost race is asserted as the ConcurrencyException it renders.
[Collection(BuddyApiCollection.Name)]
public sealed class AwardFlashCardStarsTests(BuddyApiFixture fixture) : IAsyncLifetime
{
    private static readonly DateOnly Day = new(2025, 6, 10);

    private readonly List<Guid> _children = [];

    private IProgressEventStore Progress => fixture.Host.Services.GetRequiredService<IProgressEventStore>();

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        foreach (var child in _children)
        {
            ConcurrentWriterProgressEventStore.Disarm(child);
        }

        return Task.CompletedTask;
    }

    private UserId NewChild()
    {
        var child = new UserId(Guid.CreateVersion7());
        _children.Add(child.Value);

        return child;
    }

    private async Task<int> AwardAsync(UserId child, Guid set, int tierStars, DateOnly? day = null, Guid? gameId = null)
    {
        await using var scope = fixture.Host.Services.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var result = await bus.InvokeAsync<AwardFlashCardStarsResult>(
            new buddy.Features.Progress.AwardFlashCardStars(child, set, day ?? Day, tierStars, gameId ?? Guid.NewGuid()),
            CancellationToken.None);

        return result.Awarded;
    }

    private async Task<ChildProgress> ReadAsync(UserId child) =>
        ChildProgress.Replay(await Progress.ReadAsync(ProgressId.ForChild(child), CancellationToken.None));

    private async Task<IReadOnlyCollection<ProgressEvent>> EventsAsync(UserId child) =>
        await Progress.ReadAsync(ProgressId.ForChild(child), CancellationToken.None);

    private static ProgressEvent Concurrent(UserId child, Guid set, int stars, DateOnly? day = null) =>
        new FlashCardStarsAwarded(ProgressId.ForChild(child), set, day ?? Day, stars, Guid.NewGuid(), DateTimeOffset.UtcNow);

    private static ProgressEvent Started(UserId child) => new ProgressStarted(ProgressId.ForChild(child), child, DateTimeOffset.UtcNow);

    [Fact]
    public async Task The_first_game_of_a_set_awards_the_whole_tier_and_creates_the_stream()
    {
        var child = NewChild();
        var set = Guid.NewGuid();

        Assert.Equal(3, await AwardAsync(child, set, 3));

        var progress = await ReadAsync(child);
        Assert.Equal(3, progress.TotalStars);
        Assert.Equal(3, progress.FlashCardStarsOn(set, Day));
        Assert.IsType<ProgressStarted>(Assert.Single(await EventsAsync(child), e => e.Value is ProgressStarted).Value);
    }

    [Fact]
    public async Task A_higher_tier_the_same_day_tops_up_by_the_difference()
    {
        var child = NewChild();
        var set = Guid.NewGuid();

        Assert.Equal(3, await AwardAsync(child, set, 3));
        Assert.Equal(4, await AwardAsync(child, set, 7));

        var progress = await ReadAsync(child);
        Assert.Equal(7, progress.TotalStars);
        Assert.Equal(7, progress.FlashCardStarsOn(set, Day));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(3)]
    [InlineData(1)]
    public async Task The_same_or_a_lower_tier_awards_nothing_and_appends_no_event(int tierStars)
    {
        var child = NewChild();
        var set = Guid.NewGuid();
        await AwardAsync(child, set, 5);
        var before = (await EventsAsync(child)).Count;

        Assert.Equal(0, await AwardAsync(child, set, tierStars));

        Assert.Equal(before, (await EventsAsync(child)).Count);
        Assert.Equal(5, (await ReadAsync(child)).TotalStars);
    }

    [Fact]
    public async Task A_new_day_resets_the_cap()
    {
        var child = NewChild();
        var set = Guid.NewGuid();

        await AwardAsync(child, set, 4);

        Assert.Equal(4, await AwardAsync(child, set, 4, Day.AddDays(1)));
        Assert.Equal(8, (await ReadAsync(child)).TotalStars);
    }

    [Fact]
    public async Task Another_set_is_capped_independently()
    {
        var child = NewChild();

        await AwardAsync(child, Guid.NewGuid(), 4);

        Assert.Equal(4, await AwardAsync(child, Guid.NewGuid(), 4));
        Assert.Equal(8, (await ReadAsync(child)).TotalStars);
    }

    [Fact]
    public async Task One_award_unlocks_every_goal_post_it_crosses_and_none_twice()
    {
        var child = NewChild();
        var set = Guid.NewGuid();

        // Default scale: 5, 10, 25. 12 stars cross 5 and 10.
        await AwardAsync(child, set, 12);
        Assert.Equal([5, 10], (await ReadAsync(child)).UnlockedMilestones.Order());

        // 15 total: nothing new. 25 total crosses 25 only.
        await AwardAsync(child, Guid.NewGuid(), 3);
        Assert.Equal([5, 10], (await ReadAsync(child)).UnlockedMilestones.Order());
        await AwardAsync(child, Guid.NewGuid(), 10);
        Assert.Equal([5, 10, 25], (await ReadAsync(child)).UnlockedMilestones.Order());

        var unlocks = (await EventsAsync(child)).Count(e => e.Value is MilestoneUnlocked);
        Assert.Equal(3, unlocks);
    }

    [Fact]
    public async Task A_goal_post_recrossed_after_a_revoked_star_is_not_unlocked_again()
    {
        var child = NewChild();
        var id = ProgressId.ForChild(child);
        await AwardAsync(child, Guid.NewGuid(), 5);
        Assert.Contains(5, (await ReadAsync(child)).UnlockedMilestones);

        // A revoked star drops the total below 5 but never removes the unlocked milestone, exactly as
        // for a task star (RecordStarChangeHandler checks UnlockedMilestones too).
        await Progress.AppendAsync(
            id,
            [new StarRevoked(id, new buddy.Features.Calendars.CalendarItemId(Guid.NewGuid()), Day, DateTimeOffset.UtcNow, new buddy.Features.Calendars.CompletionTarget.WholeTask())],
            CancellationToken.None);
        Assert.Equal(4, (await ReadAsync(child)).TotalStars);

        await AwardAsync(child, Guid.NewGuid(), 1);

        Assert.Equal(5, (await ReadAsync(child)).TotalStars);
        Assert.Single(await EventsAsync(child), e => e.Value is MilestoneUnlocked);
    }

    [Fact]
    public async Task Awarding_again_with_a_used_game_id_awards_nothing_and_the_first_award_is_found_by_that_id()
    {
        var child = NewChild();
        var set = Guid.NewGuid();
        var game = Guid.NewGuid();

        Assert.Equal(3, await AwardAsync(child, set, 3, gameId: game));
        Assert.Equal(0, await AwardAsync(child, set, 3, gameId: game));

        var found = await Progress.FindFlashCardAwardAsync(ProgressId.ForChild(child), game, CancellationToken.None);
        Assert.NotNull(found);
        Assert.Equal(3, found.Stars);
        Assert.Equal(set, found.SetId);
        Assert.Equal(Day, found.Day);
        Assert.Single(await EventsAsync(child), e => e.Value is FlashCardStarsAwarded);
    }

    [Fact]
    public async Task The_award_lookup_is_scoped_to_the_childs_own_stream()
    {
        var childA = NewChild();
        var childB = NewChild();
        var game = Guid.NewGuid();
        await AwardAsync(childA, Guid.NewGuid(), 3, gameId: game);

        Assert.Null(await Progress.FindFlashCardAwardAsync(ProgressId.ForChild(childB), game, CancellationToken.None));

        // Child B plays with the same GameId and gets an independent award.
        Assert.Equal(2, await AwardAsync(childB, Guid.NewGuid(), 2, gameId: game));
        Assert.Equal(2, (await Progress.FindFlashCardAwardAsync(ProgressId.ForChild(childB), game, CancellationToken.None))!.Stars);
        Assert.Equal(3, (await Progress.FindFlashCardAwardAsync(ProgressId.ForChild(childA), game, CancellationToken.None))!.Stars);
    }

    [Fact]
    public async Task The_award_lookup_finds_nothing_for_an_unknown_game_or_a_missing_stream()
    {
        var child = NewChild();

        Assert.Null(await Progress.FindFlashCardAwardAsync(ProgressId.ForChild(child), Guid.NewGuid(), CancellationToken.None));

        await AwardAsync(child, Guid.NewGuid(), 3);

        Assert.Null(await Progress.FindFlashCardAwardAsync(ProgressId.ForChild(child), Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task An_award_on_an_old_day_beyond_the_window_raises_the_total_but_leaves_no_cap_entry()
    {
        var child = NewChild();
        var set = Guid.NewGuid();
        await AwardAsync(child, set, 2, Day);

        Assert.Equal(3, await AwardAsync(child, set, 3, Day.AddDays(-8)));

        var progress = await ReadAsync(child);
        Assert.Equal(5, progress.TotalStars);
        Assert.Equal<FlashCardDayStars>([new FlashCardDayStars(set, Day, 2)], progress.FlashCardStarsByDay.AsEnumerable());
    }

    [Fact]
    public async Task A_second_finish_that_read_the_same_version_conflicts_and_only_one_award_lands_on_an_existing_stream()
    {
        var child = NewChild();
        var set = Guid.NewGuid();
        await AwardAsync(child, set, 1, Day.AddDays(-1));

        // The other finish lands after this request's read: same set, same day, tier stars 5.
        ConcurrentWriterProgressEventStore.InterleaveNextReads(child.Value, 1, _ => [Concurrent(child, set, 5)]);

        await AssertConflictAsync(() => AwardAsync(child, set, 5));

        // The retry (a new request) re-reads, sees the cap reached and awards 0.
        Assert.Equal(0, await AwardAsync(child, set, 5));
        Assert.Equal(6, (await ReadAsync(child)).TotalStars);
        Assert.Single(await EventsAsync(child), e => e.Value is FlashCardStarsAwarded a && a.Day == Day);
    }

    [Fact]
    public async Task A_second_first_finish_for_a_child_with_no_stream_conflicts_and_is_not_a_500()
    {
        var child = NewChild();
        var set = Guid.NewGuid();

        // Interleave a stream creation (ProgressStarted plus an award) after the read found no stream.
        ConcurrentWriterProgressEventStore.InterleaveNextReads(child.Value, 1, _ => [Started(child), Concurrent(child, set, 5)]);

        // The collision of the two stream starts must surface as a ConcurrencyException (409), never
        // as Marten's ExistingStreamIdCollisionException (500).
        await AssertConflictAsync(() => AwardAsync(child, set, 5));

        Assert.Equal(0, await AwardAsync(child, set, 5));
        Assert.Equal(5, (await ReadAsync(child)).TotalStars);
    }

    [Fact]
    public async Task When_the_retry_conflicts_too_the_third_attempt_awards_nothing_and_the_total_is_one_tier()
    {
        var child = NewChild();
        var set = Guid.NewGuid();
        await AwardAsync(child, set, 1, Day.AddDays(-1));

        // Fires on the first two reads. The first request loses to 2 stars, so its retry (which now
        // sees 2 already paid) would award 3 and loses to the other 3; the third attempt sees the
        // cap of 5 reached.
        ConcurrentWriterProgressEventStore.InterleaveNextReads(child.Value, 2, n => [Concurrent(child, set, n == 0 ? 2 : 3)]);

        await AssertConflictAsync(() => AwardAsync(child, set, 5));
        await AssertConflictAsync(() => AwardAsync(child, set, 5));

        Assert.Equal(0, await AwardAsync(child, set, 5));
        Assert.Equal(1 + 5, (await ReadAsync(child)).TotalStars);
    }

    [Fact]
    public async Task Two_sets_finishing_at_once_conflict_and_the_retry_awards_its_own_set()
    {
        var child = NewChild();
        var setA = Guid.NewGuid();
        var setB = Guid.NewGuid();
        await AwardAsync(child, Guid.NewGuid(), 1, Day.AddDays(-1));

        ConcurrentWriterProgressEventStore.InterleaveNextReads(child.Value, 1, _ => [Concurrent(child, setB, 4)]);

        await AssertConflictAsync(() => AwardAsync(child, setA, 3));

        Assert.Equal(3, await AwardAsync(child, setA, 3));
        Assert.Equal(1 + 4 + 3, (await ReadAsync(child)).TotalStars);
    }

    [Fact]
    public async Task Parallel_first_finishes_award_exactly_once_and_never_fail_with_a_server_error()
    {
        var child = NewChild();
        var set = Guid.NewGuid();

        var attempts = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            try
            {
                return await AwardAsync(child, set, 5);
            }
            catch (Exception exception) when (HasConcurrencyConflict(exception))
            {
                return -1;
            }
        }));

        // Whichever way the race went, the cap lets exactly one award of 5 land.
        Assert.Equal(5, (await ReadAsync(child)).TotalStars);
        Assert.Contains(5, attempts);
    }

    private static bool HasConcurrencyConflict(Exception? exception)
    {
        for (; exception is not null; exception = exception.InnerException)
        {
            if (exception is ConcurrencyException)
            {
                return true;
            }
        }

        return false;
    }

    private static async Task AssertConflictAsync(Func<Task> action)
    {
        var exception = await Record.ExceptionAsync(action);

        Assert.NotNull(exception);
        Assert.True(HasConcurrencyConflict(exception), $"Expected a ConcurrencyException, got {exception}");
    }
}
