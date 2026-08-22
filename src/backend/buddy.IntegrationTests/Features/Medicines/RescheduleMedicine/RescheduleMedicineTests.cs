using Alba;

using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.Medicines;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Medicines.RescheduleMedicine;

[Collection(BuddyApiCollection.Name)]
public sealed class RescheduleMedicineTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("RescheduleMedicine")]
    public async Task A_guardian_can_change_dose_times_and_the_course_window()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var schedule = await MedicineTestHelpers.CreateMedicineScheduleAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(schedule);

        var newStart = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var newEnd = newStart.AddDays(7);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Patch.Json(new { Times = new[] { new TimeOnly(9, 0) }, StartDate = newStart, EndDate = newEnd })
                .ToUrl($"/medicines/children/{child.Id}/schedules/{schedule.Id}/schedule");
            _.StatusCodeShouldBeOk();
        });

        var updated = response.ReadAsJson<MedicineScheduleDto>();
        Assert.Equal([new TimeOnly(9, 0)], updated.Times);
        Assert.Equal(newStart, updated.StartDate);
        Assert.Equal(newEnd, updated.EndDate);
    }

    [Fact]
    public async Task An_end_date_before_the_start_date_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var schedule = await MedicineTestHelpers.CreateMedicineScheduleAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(schedule);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Patch.Json(new { Times = new[] { new TimeOnly(9, 0) }, StartDate = today, EndDate = today.AddDays(-1) })
                .ToUrl($"/medicines/children/{child.Id}/schedules/{schedule.Id}/schedule");
            _.StatusCodeShouldBe(400);
        });
    }
}
