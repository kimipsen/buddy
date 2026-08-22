using Alba;

using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.Medicines;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Medicines.CreateMedicineSchedule;

[Collection(BuddyApiCollection.Name)]
public sealed class CreateMedicineScheduleTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("CreateMedicineSchedule")]
    public async Task A_guardian_can_create_a_medicine_schedule_for_their_child()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        var schedule = await MedicineTestHelpers.CreateMedicineScheduleAsync(fixture, guardianToken, child.Id);

        Assert.NotNull(schedule);
        Assert.Equal("Amoxicillin", schedule.Name);
        Assert.Equal(2, schedule.Times.Count);
        Assert.False(schedule.IsStopped);
    }

    [Fact]
    public async Task A_schedule_with_no_dose_times_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        await MedicineTestHelpers.CreateMedicineScheduleAsync(
            fixture, guardianToken, child.Id, new CreateMedicineScheduleOptions(Times: []), expectedStatus: 400);
    }

    [Fact]
    public async Task An_end_date_before_the_start_date_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await MedicineTestHelpers.CreateMedicineScheduleAsync(
            fixture, guardianToken, child.Id,
            new CreateMedicineScheduleOptions(StartDate: today, EndDate: today.AddDays(-1)),
            expectedStatus: 400);
    }

    [Fact]
    public async Task The_child_cannot_create_their_own_medicine_schedule()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        await MedicineTestHelpers.CreateMedicineScheduleAsync(fixture, childToken, child.Id, expectedStatus: 403);
    }

    [Fact]
    public async Task A_third_party_with_no_guardian_link_gets_not_found()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await MedicineTestHelpers.CreateMedicineScheduleAsync(fixture, strangerToken, child.Id, expectedStatus: 404);
    }
}
