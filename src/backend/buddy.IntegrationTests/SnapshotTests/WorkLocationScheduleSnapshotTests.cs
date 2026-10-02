using Alba;

using buddy.Features.Users;
using buddy.Features.WorkLocations;
using buddy.IntegrationTests.Features.WorkLocations;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.SnapshotTests;

// Verifies the inline WorkLocationScheduleSnapshotProjection stays exactly consistent with a full
// replay after commands covering every event type -- see PickupScheduleSnapshotTests.
[Collection(BuddyApiCollection.Name)]
public sealed class WorkLocationScheduleSnapshotTests(BuddyApiFixture fixture)
{
    [Fact]
    public async Task The_snapshot_matches_a_full_replay_after_several_commands()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var monday = new DateOnly(2026, 9, 28);

        var stil = await WorkLocationTestHelpers.AddLocationAsync(fixture, token, "Stil");
        var randers = await WorkLocationTestHelpers.AddLocationAsync(fixture, token, "Randers");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Name = "Kontoret", Icon = "🏬", Color = "#16a34a" }).ToUrl($"/work-locations/me/locations/{stil.Id}");
            _.StatusCodeShouldBeOk();
        });

        await WorkLocationTestHelpers.ReplacePatternAsync(fixture, token, 2, monday,
            new PatternDayDto(0, DayOfWeek.Tuesday, stil.Id),
            new PatternDayDto(1, DayOfWeek.Friday, randers.Id));
        await WorkLocationTestHelpers.SetOverridesAsync(fixture, token, monday, monday.AddDays(2), stil.Id);
        await WorkLocationTestHelpers.SetOverridesAsync(fixture, token, monday.AddDays(5), monday.AddDays(5), locationId: null);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url($"/work-locations/me/overrides?from={monday:yyyy-MM-dd}&to={monday:yyyy-MM-dd}");
            _.StatusCodeShouldBe(204);
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url($"/work-locations/me/locations/{randers.Id}");
            _.StatusCodeShouldBe(204);
        });

        var store = fixture.Host.Services.GetRequiredService<IWorkLocationScheduleEventStore>();
        var id = WorkLocationScheduleId.ForGuardian(new UserId(guardianId));

        var replayed = WorkLocationSchedule.Rehydrate(await store.ReadAsync(id, CancellationToken.None));
        var snapshot = await store.FindSnapshotAsync(id, CancellationToken.None);

        Assert.NotNull(replayed);
        Assert.NotNull(snapshot);
        Assert.Equal(3, replayed.Overrides.Count);
        Assert.Equivalent(replayed, snapshot, strict: true);
    }
}
