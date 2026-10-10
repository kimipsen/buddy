using buddy.Features.HouseRules;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.HouseRules.HouseRulesTestHelpers;

namespace buddy.IntegrationTests.SnapshotTests;

// The inline RuleBookSnapshotProjection must stay exactly equal to a full replay -- see
// docs/backend/analysis/event-stream-snapshots.md.
[Collection(BuddyApiCollection.Name)]
public sealed class RuleBookSnapshotTests(BuddyApiFixture fixture)
{
    [Fact]
    public async Task The_rule_book_snapshot_matches_a_full_replay_after_every_kind_of_event()
    {
        var home = await CreateHouseholdAsync(fixture);
        var a = await AddOneRuleAsync(fixture, home.OwnerToken, home.Rules, "A");
        var b = await AddOneRuleAsync(fixture, home.OwnerToken, home.Rules, "B");
        var c = await AddOneRuleAsync(fixture, home.OwnerToken, home.Rules, "C");
        await AcknowledgeAsync(fixture, home.ChildToken, home.Rules, a.Id, 1);
        await AcknowledgeAsync(fixture, home.ChildToken, home.Rules, b.Id, 1);
        await EditRuleAsync(fixture, home.OwnerToken, home.Rules, a.Id, "A", "changed");
        await EditRuleAsync(fixture, home.OwnerToken, home.Rules, b.Id, "B!", b.Body, requireReacknowledgement: false);
        await RemoveRuleAsync(fixture, home.OwnerToken, home.Rules, c.Id);
        await ReorderRulesAsync(fixture, home.OwnerToken, home.Rules, [b.Id, a.Id]);

        var books = fixture.Host.Services.GetRequiredService<IRuleBookEventStore>();
        var id = new RuleBookId(home.GroupId);

        var replayed = RuleBook.Rehydrate(await books.ReadAsync(id, CancellationToken.None));
        var snapshot = await books.FindSnapshotAsync(id, CancellationToken.None);

        Assert.NotNull(replayed);
        Assert.NotNull(snapshot);
        Assert.Equal(2, replayed.Acknowledgements.Count);
        Assert.Equivalent(replayed, snapshot, strict: true);
    }
}
