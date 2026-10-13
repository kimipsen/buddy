using buddy.Features.Feelings;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.Feelings.FeelingsTestHelpers;

namespace buddy.IntegrationTests.SnapshotTests;

// The inline snapshots (FeelingsJournalSnapshotProjection, FeelingsShareTokenSnapshotProjection)
// must stay exactly equal to a full replay -- see docs/backend/analysis/event-stream-snapshots.md.
[Collection(BuddyApiCollection.Name)]
public sealed class FeelingsSnapshotTests(BuddyApiFixture fixture)
{
    [Fact]
    public async Task The_journal_snapshot_matches_a_full_replay_after_every_kind_of_event()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Emil");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var day = new DateOnly(2026, 3, 2);

        await SetPromptTimesAsync(fixture, token, child.Id, ["07:30", "15:00"]);
        var own = await CheckInAsync(fixture, childToken, new { Zone = "Yellow", Tags = new[] { "Worried" } });
        var kept = await CheckInAsync(fixture, childToken, new { Zone = "Green" });
        var observation = await LogAsync(fixture, token, child.Id, new { Zone = "Red", Date = day, Time = "15:10" });
        await EditAsync(fixture, token, child.Id, observation!.Id, new { Zone = "Yellow", Tags = new[] { "Frustrated" }, Note = "Calmer", Date = day, Time = "15:30" });
        await UndoAsync(fixture, childToken, own!.Id);
        await SetDisplayModeAsync(fixture, token, child.Id, "FacesOnly");

        var journals = fixture.Host.Services.GetRequiredService<IFeelingsJournalEventStore>();
        var id = FeelingsJournalId.ForChild(new UserId(child.Id));

        var replayed = FeelingsJournal.Rehydrate(await journals.ReadAsync(id, CancellationToken.None));
        var snapshot = await journals.FindSnapshotAsync(id, CancellationToken.None);

        Assert.NotNull(replayed);
        Assert.NotNull(snapshot);
        Assert.Equal(2, replayed.CheckIns.Count);
        Assert.Contains(new FeelingCheckInId(kept!.Id), replayed.CheckIns.Keys);
        Assert.Equal(FeelingsDisplayMode.FacesOnly, replayed.DisplayMode);
        Assert.Equivalent(replayed, snapshot, strict: true);
    }

    [Fact]
    public async Task The_share_token_snapshot_matches_a_full_replay_after_revoking()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Emil");
        var link = await CreateShareLinkAsync(fixture, token, child.Id, DateTimeOffset.UtcNow.AddDays(30));
        await RevokeShareLinkAsync(fixture, token, child.Id, link!.Id);

        var shareTokens = fixture.Host.Services.GetRequiredService<IFeelingsShareTokenEventStore>();
        var id = new FeelingsShareTokenId(link.Id);

        var replayed = FeelingsShareToken.Rehydrate(await shareTokens.ReadAsync(id, CancellationToken.None));
        var snapshot = await shareTokens.FindSnapshotAsync(id, CancellationToken.None);

        Assert.NotNull(replayed);
        Assert.True(replayed.IsRevoked);
        Assert.Equivalent(replayed, snapshot, strict: true);
    }
}
