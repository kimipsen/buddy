using buddy.Features.FlashCards;
using buddy.Features.Users;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.SnapshotTests;

// The inline snapshot (FlashCardSetSnapshotProjection, schema "snapshots") must equal a full replay
// of the stream after every kind of event.
[Collection(BuddyApiCollection.Name)]
public sealed class FlashCardSetSnapshotTests(BuddyApiFixture fixture)
{
    [Fact]
    public async Task The_snapshot_matches_a_full_replay_after_every_kind_of_event()
    {
        var store = fixture.Host.Services.GetRequiredService<IFlashCardSetEventStore>();
        var owner = new UserId(Guid.CreateVersion7());
        var id = FlashCardSetId.New();
        var now = new DateTimeOffset(2025, 3, 1, 8, 0, 0, TimeSpan.Zero);
        var first = new FlashCard(FlashCardId.New(), "Capital of France?", "Paris");
        var second = new FlashCard(FlashCardId.New(), "Capital of Italy?", "Rome");

        await store.CreateAsync(id, [new FlashCardSetCreated(id, owner, "Capitals", FlashCardSetKind.Text, null, now)], CancellationToken.None);
        await store.AppendAsync(id, [new FlashCardAdded(id, first, owner, now), new FlashCardAdded(id, second, owner, now)], CancellationToken.None);
        await store.AppendAsync(id, [new FlashCardUpdated(id, first.Id, "Capital of Spain?", "Madrid", owner, now)], CancellationToken.None);
        await store.AppendAsync(id, [new FlashCardRemoved(id, second.Id, owner, now)], CancellationToken.None);
        await store.AppendAsync(id, [new FlashCardSetUpdated(id, "European capitals", null, owner, now)], CancellationToken.None);
        await store.AppendAsync(id, [new FlashCardSetDeleted(id, owner, now.AddDays(1))], CancellationToken.None);

        var replayed = FlashCardSet.Rehydrate(await store.ReadAsync(id, CancellationToken.None));
        var snapshot = await store.FindSnapshotAsync(id, CancellationToken.None);

        Assert.NotNull(replayed);
        Assert.NotNull(snapshot);
        Assert.Equivalent(replayed, snapshot, strict: true);
        Assert.True(snapshot.IsDeleted);
        Assert.Single(snapshot.Cards);
    }

    [Fact]
    public async Task A_math_set_round_trips_its_rule()
    {
        var store = fixture.Host.Services.GetRequiredService<IFlashCardSetEventStore>();
        var id = FlashCardSetId.New();
        var owner = new UserId(Guid.CreateVersion7());

        await store.CreateAsync(
            id,
            [new FlashCardSetCreated(id, owner, "Division by 3", FlashCardSetKind.Math, new MathRule(MathOperation.Division, 3), DateTimeOffset.UnixEpoch)],
            CancellationToken.None);

        var snapshot = await store.FindSnapshotAsync(id, CancellationToken.None);

        Assert.Equal(new MathRule(MathOperation.Division, 3), snapshot!.Math);
        Assert.Equal(FlashCardSetKind.Math, snapshot.Kind);
    }
}
