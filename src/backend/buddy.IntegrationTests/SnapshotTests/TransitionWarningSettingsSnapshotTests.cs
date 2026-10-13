using buddy.Features.TransitionWarnings;
using buddy.IntegrationTests.Features.TransitionWarnings;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.TransitionWarnings.TransitionWarningsTestHelpers;

namespace buddy.IntegrationTests.SnapshotTests;

// The inline TransitionWarningSettingsSnapshotProjection must stay exactly equal to a full replay --
// see docs/backend/analysis/event-stream-snapshots.md.
[Collection(BuddyApiCollection.Name)]
public sealed class TransitionWarningSettingsSnapshotTests(BuddyApiFixture fixture)
{
    [Fact]
    public async Task The_settings_snapshot_matches_a_full_replay_after_every_kind_of_event()
    {
        var family = await CreateFamilyAsync(fixture);
        await ConfigureAsync(fixture, family.GuardianToken, family.Child.Id,
            Settings(true, [20, 5], [WarningKind.Event, WarningKind.Task], new QuietHoursDto(new TimeOnly(21, 0), new TimeOnly(7, 0))));
        await SetOverrideAsync(fixture, family.GuardianToken, family.Child.Id, family.Event.Id, [30]);
        await SetOverrideAsync(fixture, family.GuardianToken, family.Child.Id, family.Event.Id, []);
        await ClearOverrideAsync(fixture, family.GuardianToken, family.Child.Id, family.Event.Id);
        await SetOverrideAsync(fixture, family.GuardianToken, family.Child.Id, family.Event.Id, [45]);

        var store = fixture.Host.Services.GetRequiredService<ITransitionWarningSettingsEventStore>();
        var id = new TransitionWarningSettingsId(family.Child.Id);

        var replayed = TransitionWarningSettings.Rehydrate(await store.ReadAsync(id, CancellationToken.None));
        var snapshot = await store.FindSnapshotAsync(id, CancellationToken.None);

        Assert.NotNull(replayed);
        Assert.NotNull(snapshot);
        Assert.Single(replayed.ItemOverrides);
        Assert.Equivalent(replayed, snapshot, strict: true);
    }
}
