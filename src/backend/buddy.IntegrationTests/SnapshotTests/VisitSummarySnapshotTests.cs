using buddy.Features.VisitSummaries;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.VisitSummaries.VisitSummaryTestHelpers;

namespace buddy.IntegrationTests.SnapshotTests;

// The inline VisitSummarySnapshotProjection must stay exactly equal to a full replay -- see
// docs/backend/analysis/event-stream-snapshots.md.
[Collection(BuddyApiCollection.Name)]
public sealed class VisitSummarySnapshotTests(BuddyApiFixture fixture)
{
    [Fact]
    public async Task The_snapshot_matches_a_full_replay_after_every_kind_of_event()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var summary = await CreateAsync(fixture, token, child.Id);
        await UpdateAsync(fixture, token, child.Id, summary.Id, Body(title: "Edited", sections: ["Sleep"]));
        var revoked = await CreateShareLinkAsync(fixture, token, child.Id, summary.Id, DateTimeOffset.UtcNow.AddDays(30));
        await CreateShareLinkAsync(fixture, token, child.Id, summary.Id);
        await RevokeShareLinkAsync(fixture, token, child.Id, summary.Id, revoked.Id);
        await DeleteAsync(fixture, token, child.Id, summary.Id);

        var store = fixture.Host.Services.GetRequiredService<IVisitSummaryEventStore>();
        var id = new VisitSummaryId(summary.Id);

        var replayed = VisitSummary.Rehydrate(await store.ReadAsync(id, CancellationToken.None));
        var snapshot = await store.FindSnapshotAsync(id, CancellationToken.None);

        Assert.NotNull(replayed);
        Assert.True(replayed.IsDeleted);
        Assert.All(replayed.ShareLinks, l => Assert.True(l.IsRevoked));
        Assert.Equivalent(replayed, snapshot, strict: true);
    }
}
