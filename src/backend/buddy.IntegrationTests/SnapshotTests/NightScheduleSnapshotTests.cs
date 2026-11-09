using buddy.Features.NightDuty;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Babysitters;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.NightDuty.NightDutyTestHelpers;

namespace buddy.IntegrationTests.SnapshotTests;

// Verifies the inline NightScheduleSnapshotProjection stays exactly consistent with a full replay
// after commands covering every event type and every NightResponsible case.
[Collection(BuddyApiCollection.Name)]
public sealed class NightScheduleSnapshotTests(BuddyApiFixture fixture)
{
    [Fact]
    public async Task The_snapshot_matches_a_full_replay_after_several_commands()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var maja = await BabysitterTestHelpers.AddAsync(fixture, token, "Maja");
        var monday = FutureMonday();

        await ReplacePatternAsync(fixture, token, child.Id, monday, 2, monday,
            Day(0, DayOfWeek.Monday, Guardian(guardianId)),
            Day(0, DayOfWeek.Friday, NoteOnly("Screen night")),
            Day(1, DayOfWeek.Tuesday, Babysitter(guardianId, maja.Id)));
        await ReplacePatternAsync(fixture, token, child.Id, monday.AddDays(14), 1, monday, Day(0, DayOfWeek.Saturday, Sleepover("Grandma Lis")));
        await SetOverridesAsync(fixture, token, child.Id,
            Override(monday, entry: null),
            Override(monday.AddDays(1), Guardian(guardianId, "Movie")),
            Override(monday.AddDays(2), Sleepover("Oscar's family", "Skovvej 1")));
        await ClearOverridesAsync(fixture, token, child.Id, monday, monday);

        var store = fixture.Host.Services.GetRequiredService<INightScheduleEventStore>();
        var id = NightScheduleId.ForChild(new UserId(child.Id));

        var replayed = NightSchedule.Rehydrate(await store.ReadAsync(id, CancellationToken.None));
        var snapshot = await store.FindSnapshotAsync(id, CancellationToken.None);

        Assert.NotNull(replayed);
        Assert.NotNull(snapshot);
        Assert.Equal(2, replayed.Patterns.Count);
        Assert.Equal(2, replayed.Overrides.Count);
        Assert.Equivalent(replayed, snapshot, strict: true);
    }
}
