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

    [Fact]
    public async Task An_empty_name_is_rejected()
    {
        var (guardianToken, url) = await CreateScheduleAsync();

        var response = await PatchDetailsAsync(guardianToken, url, "", "5 ml", 400);

        MedicineTestHelpers.AssertValidationError(response, "Name");
    }

    [Fact]
    public async Task A_name_of_200_characters_is_accepted_but_201_is_rejected()
    {
        var (guardianToken, url) = await CreateScheduleAsync();

        await PatchDetailsAsync(guardianToken, url, new string('n', 200), "5 ml", 200);
        var response = await PatchDetailsAsync(guardianToken, url, new string('n', 201), "5 ml", 400);

        MedicineTestHelpers.AssertValidationError(response, "Name");
    }

    [Fact]
    public async Task A_dosage_of_200_characters_is_accepted_but_201_is_rejected()
    {
        var (guardianToken, url) = await CreateScheduleAsync();

        await PatchDetailsAsync(guardianToken, url, "Amoxicillin", new string('d', 200), 200);
        var response = await PatchDetailsAsync(guardianToken, url, "Amoxicillin", new string('d', 201), 400);

        MedicineTestHelpers.AssertValidationError(response, "Dosage");
    }

    [Fact]
    public async Task A_group_edit_with_an_empty_name_is_rejected()
    {
        var (guardianToken, url) = await CreateScheduleAsync(viaGroup: true);

        var response = await PatchDetailsAsync(guardianToken, url, "", "5 ml", 400);

        MedicineTestHelpers.AssertValidationError(response, "Name");
    }

    [Fact]
    public async Task A_group_edit_name_of_200_characters_is_accepted_but_201_is_rejected()
    {
        var (guardianToken, url) = await CreateScheduleAsync(viaGroup: true);

        await PatchDetailsAsync(guardianToken, url, new string('n', 200), "5 ml", 200);
        var response = await PatchDetailsAsync(guardianToken, url, new string('n', 201), "5 ml", 400);

        MedicineTestHelpers.AssertValidationError(response, "Name");
    }

    [Fact]
    public async Task A_group_edit_dosage_of_200_characters_is_accepted_but_201_is_rejected()
    {
        var (guardianToken, url) = await CreateScheduleAsync(viaGroup: true);

        await PatchDetailsAsync(guardianToken, url, "Amoxicillin", new string('d', 200), 200);
        var response = await PatchDetailsAsync(guardianToken, url, "Amoxicillin", new string('d', 201), 400);

        MedicineTestHelpers.AssertValidationError(response, "Dosage");
    }

    // Returns the schedule's details URL, either the child-keyed route or (viaGroup) the
    // group-keyed one after sharing the child's medicine with a group the guardian owns.
    private async Task<(string GuardianToken, string Url)> CreateScheduleAsync(bool viaGroup = false)
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var schedule = await MedicineTestHelpers.CreateMedicineScheduleAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(schedule);

        if (!viaGroup)
        {
            return (guardianToken, $"/medicines/children/{child.Id}/schedules/{schedule.Id}/details");
        }

        var groupId = await MedicineTestHelpers.ShareWithNewGroupAsync(fixture, guardianToken, child.Id);
        return (guardianToken, $"/medicines/groups/{groupId}/children/{child.Id}/schedules/{schedule.Id}/details");
    }

    private Task<IScenarioResult> PatchDetailsAsync(string token, string url, string name, string dosage, int expectedStatus) =>
        fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Name = name, Dosage = dosage, Icon = "pill", Color = "#ff8800" }).ToUrl(url);
            _.StatusCodeShouldBe(expectedStatus);
        });
}
