using Alba;

using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.Medicines;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Medicines.UpdateMedicineDetails;

[Collection(BuddyApiCollection.Name)]
public sealed class UpdateMedicineDetailsTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("UpdateMedicineDetails")]
    public async Task A_guardian_can_update_a_schedules_name_dosage_icon_and_color()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var schedule = await MedicineTestHelpers.CreateMedicineScheduleAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(schedule);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Patch.Json(new { Name = "Amoxicillin (renamed)", Dosage = "10 ml", Icon = "capsule", Color = "#00aaff" })
                .ToUrl($"/medicines/children/{child.Id}/schedules/{schedule.Id}/details");
            _.StatusCodeShouldBeOk();
        });

        var updated = response.ReadAsJson<MedicineScheduleDto>();
        Assert.Equal("Amoxicillin (renamed)", updated.Name);
        Assert.Equal("10 ml", updated.Dosage);
        Assert.Equal("capsule", updated.Icon);
        Assert.Equal("#00aaff", updated.Color);
    }

    [Fact]
    public async Task The_child_cannot_edit_their_own_schedule()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var schedule = await MedicineTestHelpers.CreateMedicineScheduleAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(schedule);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Patch.Json(new { Name = "Hacked", Dosage = "5 ml", Icon = "pill", Color = "#ff8800" })
                .ToUrl($"/medicines/children/{child.Id}/schedules/{schedule.Id}/details");
            _.StatusCodeShouldBe(403);
        });
    }
}
