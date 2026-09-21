using Alba;

using buddy.Features.Medicines;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.Medicines;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.SnapshotTests;

// Verifies the inline Marten snapshot (MedicineScheduleSnapshotProjection, schema "snapshots")
// stays exactly consistent with a full replay-from-events rehydration after a sequence of
// commands exercising every event type in MedicineSchedule.Fold -- the same invariant
// GroupSnapshotTests checks for Group. See docs/backend/analysis/event-stream-snapshots.md.
[Collection(BuddyApiCollection.Name)]
public sealed class MedicineScheduleSnapshotTests(BuddyApiFixture fixture)
{
    [Fact]
    public async Task The_snapshot_matches_a_full_replay_after_several_commands()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var schedule = await MedicineTestHelpers.CreateMedicineScheduleAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(schedule);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Patch.Json(new { Name = "Amoxicillin (updated)", Dosage = "10 ml", Icon = "pill", Color = "#00ff00" })
                .ToUrl($"/medicines/children/{child.Id}/schedules/{schedule.Id}/details");
            _.StatusCodeShouldBeOk();
        });

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Patch.Json(new { Times = new[] { new TimeOnly(9, 0), new TimeOnly(21, 0) }, StartDate = today, EndDate = (DateOnly?)null })
                .ToUrl($"/medicines/children/{child.Id}/schedules/{schedule.Id}/schedule");
            _.StatusCodeShouldBeOk();
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { Status = DoseStatus.Taken })
                .ToUrl($"/medicines/children/{child.Id}/doses/{schedule.Id}")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("time", "09:00:00");
            _.StatusCodeShouldBeOk();
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Delete.Url($"/medicines/children/{child.Id}/schedules/{schedule.Id}");
            _.StatusCodeShouldBe(204);
        });

        var medicines = fixture.Host.Services.GetRequiredService<IMedicineEventStore>();
        var id = new MedicineId(schedule.Id);

        var events = await medicines.ReadAsync(id, CancellationToken.None);
        var replayed = MedicineSchedule.Rehydrate(events);
        var snapshot = await medicines.FindSnapshotAsync(id, CancellationToken.None);

        Assert.NotNull(replayed);
        Assert.NotNull(snapshot);
        Assert.Equivalent(replayed, snapshot, strict: true);
    }
}
