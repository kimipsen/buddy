using Alba;

using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.Medicines;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Medicines.StopMedicineSchedule;

[Collection(BuddyApiCollection.Name)]
public sealed class StopMedicineScheduleTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("StopMedicineSchedule")]
    public async Task Stopping_a_schedule_removes_it_from_the_childs_upcoming_doses()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var schedule = await MedicineTestHelpers.CreateMedicineScheduleAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(schedule);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Delete.Url($"/medicines/children/{child.Id}/schedules/{schedule.Id}");
            _.StatusCodeShouldBe(204);
        });

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/medicines/children/{child.Id}/doses?from={today:yyyy-MM-dd}&to={today.AddDays(1):yyyy-MM-dd}");
            _.StatusCodeShouldBeOk();
        });

        Assert.Empty(response.ReadAsJson<List<MedicineDoseOccurrenceDto>>());

        // Stopped schedules still appear in the management list -- a guardian's history of the
        // child's courses, not just the active ones.
        var listResponse = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/medicines/children/{child.Id}/schedules");
            _.StatusCodeShouldBeOk();
        });

        var listed = Assert.Single(listResponse.ReadAsJson<List<MedicineScheduleDto>>(), s => s.Id == schedule.Id);
        Assert.True(listed.IsStopped);
    }

    [Fact]
    public async Task The_child_cannot_stop_their_own_schedule()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var schedule = await MedicineTestHelpers.CreateMedicineScheduleAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(schedule);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Delete.Url($"/medicines/children/{child.Id}/schedules/{schedule.Id}");
            _.StatusCodeShouldBe(403);
        });
    }
}
