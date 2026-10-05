using buddy.Features.SleepDiaries;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.SleepDiaries.SleepDiaryTestHelpers;

namespace buddy.IntegrationTests.SnapshotTests;

// The inline snapshots (SleepDiarySnapshotProjection, SleepDiaryShareTokenSnapshotProjection) must
// stay exactly equal to a full replay -- see docs/backend/analysis/event-stream-snapshots.md.
[Collection(BuddyApiCollection.Name)]
public sealed class SleepDiarySnapshotTests(BuddyApiFixture fixture)
{
    [Fact]
    public async Task The_diary_snapshot_matches_a_full_replay_after_several_commands()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");

        await LogAsync(fixture, token, child.Id, Monday, FullNight());
        await LogAsync(fixture, token, child.Id, Monday.AddDays(1), FullNight());
        await LogAsync(fixture, token, child.Id, Monday, new { BedTime = "21:00" });
        await ClearAsync(fixture, token, child.Id, Monday.AddDays(1));
        await UpdateNotesAsync(fixture, token, child.Id, "Blackout curtains");

        var diaries = fixture.Host.Services.GetRequiredService<ISleepDiaryEventStore>();
        var id = SleepDiaryId.ForChild(new UserId(child.Id));

        var replayed = SleepDiary.Rehydrate(await diaries.ReadAsync(id, CancellationToken.None));
        var snapshot = await diaries.FindSnapshotAsync(id, CancellationToken.None);

        Assert.NotNull(replayed);
        Assert.NotNull(snapshot);
        Assert.Equivalent(replayed, snapshot, strict: true);
    }

    [Fact]
    public async Task The_share_token_snapshot_matches_a_full_replay_after_revoking()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        var link = await CreateShareLinkAsync(fixture, token, child.Id, DateTimeOffset.UtcNow.AddDays(30));
        await RevokeShareLinkAsync(fixture, token, child.Id, link!.Id);

        var shareTokens = fixture.Host.Services.GetRequiredService<ISleepDiaryShareTokenEventStore>();
        var id = new SleepDiaryShareTokenId(link.Id);

        var replayed = SleepDiaryShareToken.Rehydrate(await shareTokens.ReadAsync(id, CancellationToken.None));
        var snapshot = await shareTokens.FindSnapshotAsync(id, CancellationToken.None);

        Assert.NotNull(replayed);
        Assert.True(replayed.IsRevoked);
        Assert.Equivalent(replayed, snapshot, strict: true);
    }
}
