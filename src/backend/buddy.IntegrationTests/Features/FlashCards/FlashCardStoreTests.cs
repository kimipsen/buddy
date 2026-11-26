using buddy.Common.Concurrency;
using buddy.Features.FlashCards;
using buddy.Features.Users;
using buddy.IntegrationTests.Fixtures;

using JasperFx;

using Marten;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.Features.FlashCards;

[Collection(BuddyApiCollection.Name)]
public sealed class FlashCardStoreTests(BuddyApiFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2025, 3, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Creating_a_practice_stream_twice_is_a_concurrency_conflict_not_a_collision_error()
    {
        var store = fixture.Host.Services.GetRequiredService<IFlashCardPracticeEventStore>();
        var child = new UserId(Guid.CreateVersion7());
        var id = FlashCardPracticeId.ForChild(child);

        await store.CreateAsync(id, [new FlashCardPracticeStarted(id, child, Now)], CancellationToken.None);

        await Assert.ThrowsAsync<ConcurrencyException>(
            () => store.CreateAsync(id, [new FlashCardPracticeStarted(id, child, Now)], CancellationToken.None));
    }

    [Fact]
    public async Task Creating_a_set_stream_twice_is_a_concurrency_conflict_and_writes_no_second_index_row()
    {
        var store = fixture.Host.Services.GetRequiredService<IFlashCardSetEventStore>();
        var owner = new UserId(Guid.CreateVersion7());
        var id = FlashCardSetId.New();
        FlashCardSetEvent[] created = [new FlashCardSetCreated(id, owner, "Capitals", FlashCardSetKind.Text, null, Now)];

        await store.CreateAsync(id, created, CancellationToken.None);

        await Assert.ThrowsAsync<ConcurrencyException>(() => store.CreateAsync(id, created, CancellationToken.None));
        Assert.Equal([id], await store.ListIdsByOwnersAsync([owner], CancellationToken.None));
    }

    [Fact]
    public async Task The_first_event_of_a_new_stream_must_be_its_creation_event()
    {
        var sets = fixture.Host.Services.GetRequiredService<IFlashCardSetEventStore>();
        var practices = fixture.Host.Services.GetRequiredService<IFlashCardPracticeEventStore>();
        var id = FlashCardSetId.New();
        var practiceId = FlashCardPracticeId.ForChild(new UserId(Guid.CreateVersion7()));
        var user = new UserId(Guid.CreateVersion7());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sets.CreateAsync(id, [new FlashCardSetDeleted(id, user, Now)], CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => practices.CreateAsync(practiceId, [new FlashCardSetDisabled(practiceId, id, user, Now)], CancellationToken.None));
    }

    [Fact]
    public async Task Sets_are_listed_by_owner_and_an_empty_owner_list_returns_nothing()
    {
        var store = fixture.Host.Services.GetRequiredService<IFlashCardSetEventStore>();
        var ownerA = new UserId(Guid.CreateVersion7());
        var ownerB = new UserId(Guid.CreateVersion7());
        var other = new UserId(Guid.CreateVersion7());
        var a = FlashCardSetId.New();
        var b = FlashCardSetId.New();
        var c = FlashCardSetId.New();

        await store.CreateAsync(a, [new FlashCardSetCreated(a, ownerA, "A", FlashCardSetKind.Text, null, Now)], CancellationToken.None);
        await store.CreateAsync(b, [new FlashCardSetCreated(b, ownerB, "B", FlashCardSetKind.Text, null, Now)], CancellationToken.None);
        await store.CreateAsync(c, [new FlashCardSetCreated(c, other, "C", FlashCardSetKind.Text, null, Now)], CancellationToken.None);

        Assert.Equal([a, b], (await store.ListIdsByOwnersAsync([ownerA, ownerB], CancellationToken.None)).OrderBy(i => i.Value));
        Assert.Empty(await store.ListIdsByOwnersAsync([], CancellationToken.None));
    }

    [Fact]
    public async Task A_missing_stream_has_no_version_and_snapshot()
    {
        var store = fixture.Host.Services.GetRequiredService<IFlashCardPracticeEventStore>();
        var id = FlashCardPracticeId.ForChild(new UserId(Guid.CreateVersion7()));

        Assert.Null(await store.ReadVersionAsync(id, CancellationToken.None));
        Assert.Null(await store.FindSnapshotAsync(id, CancellationToken.None));
        Assert.Empty(await store.ReadAsync(id, CancellationToken.None));
    }

    [Fact]
    public async Task The_version_read_makes_the_next_append_an_expected_version_append()
    {
        var store = fixture.Host.Services.GetRequiredService<IFlashCardPracticeEventStore>();
        var child = new UserId(Guid.CreateVersion7());
        var guardian = new UserId(Guid.CreateVersion7());
        var id = FlashCardPracticeId.ForChild(child);
        var set = FlashCardSetId.New();
        var tier = new FlashCardTier(FlashCardTierId.New(), 10, 1);

        await store.CreateAsync(id, [new FlashCardPracticeStarted(id, child, Now)], CancellationToken.None);

        using var scope = StreamVersionTracker.BeginScope();

        Assert.Equal(1, await store.ReadVersionAsync(id, CancellationToken.None));

        // A concurrent writer (an untracked session, like another request) commits after our version read ...
        await using (var other = fixture.Host.Services.GetRequiredService<IFlashCardsStore>().LightweightSession())
        {
            other.Events.Append(id.Value, new FlashCardSetEnabled(id, set, "Other", [tier], guardian, Now));
            await other.SaveChangesAsync();
        }

        // ... so the append decided at version 1 loses instead of silently interleaving.
        await Assert.ThrowsAnyAsync<ConcurrencyException>(
            () => store.AppendAsync(id, [new FlashCardSetDisabled(id, set, guardian, Now)], CancellationToken.None));
    }

    [Fact]
    public async Task A_version_read_before_the_snapshot_read_means_the_snapshot_is_never_older_than_the_version()
    {
        var store = fixture.Host.Services.GetRequiredService<IFlashCardPracticeEventStore>();
        var child = new UserId(Guid.CreateVersion7());
        var guardian = new UserId(Guid.CreateVersion7());
        var id = FlashCardPracticeId.ForChild(child);
        var set = FlashCardSetId.New();

        await store.CreateAsync(id, [new FlashCardPracticeStarted(id, child, Now)], CancellationToken.None);
        await store.AppendAsync(id, [new FlashCardSetEnabled(id, set, "One", [new FlashCardTier(FlashCardTierId.New(), 5, 1)], guardian, Now)], CancellationToken.None);

        var version = await store.ReadVersionAsync(id, CancellationToken.None);
        var snapshot = await store.FindSnapshotAsync(id, CancellationToken.None);

        Assert.Equal(2, version);
        Assert.Equal([set], snapshot!.EnabledSets.Select(s => s.SetId));
    }
}
