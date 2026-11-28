using System.Text.Json;
using System.Text.Json.Nodes;

using buddy.Features.FlashCards;
using buddy.Features.Users;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.SnapshotTests;

[Collection(BuddyApiCollection.Name)]
public sealed class FlashCardPracticeSnapshotTests(BuddyApiFixture fixture)
{
    [Fact]
    public async Task The_snapshot_matches_a_full_replay_after_every_kind_of_event()
    {
        var store = fixture.Host.Services.GetRequiredService<IFlashCardPracticeEventStore>();
        var child = new UserId(Guid.CreateVersion7());
        var guardian = new UserId(Guid.CreateVersion7());
        var id = FlashCardPracticeId.ForChild(child);
        var setA = FlashCardSetId.New();
        var setB = FlashCardSetId.New();
        var tier = new FlashCardTier(FlashCardTierId.New(), 10, 1);
        var now = new DateTimeOffset(2025, 3, 1, 8, 0, 0, TimeSpan.Zero);

        await store.CreateAsync(id, [new FlashCardPracticeStarted(id, child, now)], CancellationToken.None);
        await store.AppendAsync(id, [new FlashCardSetEnabled(id, setA, "Times 7", [tier], guardian, now)], CancellationToken.None);
        await store.AppendAsync(id, [new FlashCardSetEnabled(id, setB, "Capitals", [tier, new FlashCardTier(FlashCardTierId.New(), 20, 2)], guardian, now)], CancellationToken.None);
        await store.AppendAsync(
            id,
            [new FlashCardGameFinished(
                id, Guid.CreateVersion7(), setA, "Times 7", tier.Id, new DateOnly(2025, 3, 1), 2,
                [new FlashCardResult(0, "mul:7x3", "7 x 3", "21", true, 1), new FlashCardResult(1, "mul:7x4", "7 x 4", "28", false, 2)],
                1, now)],
            CancellationToken.None);
        await store.AppendAsync(id, [new FlashCardSetDisabled(id, setB, guardian, now)], CancellationToken.None);

        var replayed = FlashCardPractice.Rehydrate(await store.ReadAsync(id, CancellationToken.None));
        var snapshot = await store.FindSnapshotAsync(id, CancellationToken.None);

        Assert.NotNull(replayed);
        Assert.NotNull(snapshot);
        Assert.Equivalent(replayed, snapshot, strict: true);
        Assert.Equal([setA], snapshot.EnabledSets.Select(s => s.SetId));
        Assert.Equal(1, Assert.Single(snapshot.Games).KnownCount);
        Assert.Equal(2, snapshot.CardWeights.Count);
        Assert.Equal(CardWeighting.Floor + 1, snapshot.CardWeights.Single(weight => weight.CardKey == "mul:7x3").Weight);
        Assert.Equal(CardWeighting.BaseWeight + CardWeighting.Boost, snapshot.CardWeights.Single(weight => weight.CardKey == "mul:7x4").Weight);
    }

    [Fact]
    public void A_snapshot_without_card_weights_deserialises_with_an_empty_list()
    {
        var child = new UserId(Guid.CreateVersion7());
        var practice = new FlashCardPractice(FlashCardPracticeId.ForChild(child), child, [], [], []);
        var json = JsonNode.Parse(JsonSerializer.Serialize(practice))!.AsObject();
        Assert.True(json.Remove(nameof(FlashCardPractice.CardWeights)));

        var deserialised = JsonSerializer.Deserialize<FlashCardPractice>(json);

        Assert.NotNull(deserialised);
        Assert.Empty(deserialised.CardWeights);
    }
}
