using Alba;

using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.Medicines;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Medicines.ListMedicineSchedules;

[Collection(BuddyApiCollection.Name)]
public sealed class ListMedicineSchedulesTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ListMedicineSchedules")]
    public async Task Lists_every_schedule_created_for_the_child()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        await MedicineTestHelpers.CreateMedicineScheduleAsync(fixture, guardianToken, child.Id, new CreateMedicineScheduleOptions(Name: "Amoxicillin"));
        await MedicineTestHelpers.CreateMedicineScheduleAsync(fixture, guardianToken, child.Id, new CreateMedicineScheduleOptions(Name: "Ibuprofen"));

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/medicines/children/{child.Id}/schedules");
            _.StatusCodeShouldBeOk();
        });

        var schedules = response.ReadAsJson<List<MedicineScheduleDto>>();
        Assert.Equal(2, schedules.Count);
        Assert.Contains(schedules, s => s.Name == "Amoxicillin");
        Assert.Contains(schedules, s => s.Name == "Ibuprofen");
    }

    [Fact]
    public async Task The_child_cannot_list_their_own_schedules()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Get.Url($"/medicines/children/{child.Id}/schedules");
            _.StatusCodeShouldBe(403);
        });
    }
}
