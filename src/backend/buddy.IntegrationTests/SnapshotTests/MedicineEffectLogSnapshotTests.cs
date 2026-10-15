using buddy.Features.Medicines;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.Medicines.MedicineEffectNoteTestHelpers;

namespace buddy.IntegrationTests.SnapshotTests;

// The inline MedicineEffectLogSnapshotProjection must stay exactly equal to a full replay -- see
// docs/backend/analysis/event-stream-snapshots.md.
[Collection(BuddyApiCollection.Name)]
public sealed class MedicineEffectLogSnapshotTests(BuddyApiFixture fixture)
{
    [Fact]
    public async Task The_effect_log_snapshot_matches_a_full_replay_after_several_commands()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");

        await LogAsync(fixture, token, child.Id, Monday, GoodFocusPoorAppetite());
        await LogAsync(fixture, token, child.Id, Monday.AddDays(1), GoodFocusPoorAppetite());
        await LogAsync(fixture, token, child.Id, Monday, new { Note = "Rewritten" });
        await ClearAsync(fixture, token, child.Id, Monday.AddDays(1));

        var logs = fixture.Host.Services.GetRequiredService<IMedicineEffectLogEventStore>();
        var id = MedicineEffectLogId.ForChild(new UserId(child.Id));

        var replayed = MedicineEffectLog.Rehydrate(await logs.ReadAsync(id, CancellationToken.None));
        var snapshot = await logs.FindSnapshotAsync(id, CancellationToken.None);

        Assert.NotNull(replayed);
        Assert.NotNull(snapshot);
        Assert.Equivalent(replayed, snapshot, strict: true);
    }
}
