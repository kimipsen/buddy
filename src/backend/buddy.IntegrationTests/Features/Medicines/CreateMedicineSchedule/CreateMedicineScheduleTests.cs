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

        var response = await PostScheduleAsync(guardianToken, ChildUrl(child.Id), Body(times: []), 400);

        MedicineTestHelpers.AssertValidationError(response, "Times");
    }

    [Fact]
    public async Task An_end_date_before_the_start_date_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var response = await PostScheduleAsync(guardianToken, ChildUrl(child.Id), Body(startDate: today, endDate: today.AddDays(-1)), 400);

        MedicineTestHelpers.AssertValidationError(response, "EndDate");
    }

    [Fact]
    public async Task An_end_date_on_the_start_date_is_accepted()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var response = await PostScheduleAsync(guardianToken, ChildUrl(child.Id), Body(startDate: today, endDate: today), 200);

        Assert.Equal(today, response.ReadAsJson<MedicineScheduleDto>().EndDate);
    }

    [Fact]
    public async Task An_empty_name_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        var response = await PostScheduleAsync(guardianToken, ChildUrl(child.Id), Body(name: ""), 400);

        MedicineTestHelpers.AssertValidationError(response, "Name");
    }

    [Fact]
    public async Task A_name_of_200_characters_is_accepted_but_201_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        await PostScheduleAsync(guardianToken, ChildUrl(child.Id), Body(name: new string('n', 200)), 200);
        var response = await PostScheduleAsync(guardianToken, ChildUrl(child.Id), Body(name: new string('n', 201)), 400);

        MedicineTestHelpers.AssertValidationError(response, "Name");
    }

    [Fact]
    public async Task A_dosage_of_200_characters_is_accepted_but_201_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        await PostScheduleAsync(guardianToken, ChildUrl(child.Id), Body(dosage: new string('d', 200)), 200);
        var response = await PostScheduleAsync(guardianToken, ChildUrl(child.Id), Body(dosage: new string('d', 201)), 400);

        MedicineTestHelpers.AssertValidationError(response, "Dosage");
    }

    [Fact]
    public async Task A_group_schedule_with_no_dose_times_is_rejected()
    {
        var (guardianToken, groupUrl) = await CreateSharedGroupAsync();

        var response = await PostScheduleAsync(guardianToken, groupUrl, Body(times: []), 400);

        MedicineTestHelpers.AssertValidationError(response, "Times");
    }

    [Fact]
    public async Task A_group_schedule_with_an_end_date_before_the_start_date_is_rejected_but_the_same_day_is_accepted()
    {
        var (guardianToken, groupUrl) = await CreateSharedGroupAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await PostScheduleAsync(guardianToken, groupUrl, Body(startDate: today, endDate: today), 200);
        var response = await PostScheduleAsync(guardianToken, groupUrl, Body(startDate: today, endDate: today.AddDays(-1)), 400);

        MedicineTestHelpers.AssertValidationError(response, "EndDate");
    }

    [Fact]
    public async Task A_group_schedule_with_an_empty_name_is_rejected()
    {
        var (guardianToken, groupUrl) = await CreateSharedGroupAsync();

        var response = await PostScheduleAsync(guardianToken, groupUrl, Body(name: ""), 400);

        MedicineTestHelpers.AssertValidationError(response, "Name");
    }

    [Fact]
    public async Task A_group_schedule_name_of_200_characters_is_accepted_but_201_is_rejected()
    {
        var (guardianToken, groupUrl) = await CreateSharedGroupAsync();

        await PostScheduleAsync(guardianToken, groupUrl, Body(name: new string('n', 200)), 200);
        var response = await PostScheduleAsync(guardianToken, groupUrl, Body(name: new string('n', 201)), 400);

        MedicineTestHelpers.AssertValidationError(response, "Name");
    }

    [Fact]
    public async Task A_group_schedule_dosage_of_200_characters_is_accepted_but_201_is_rejected()
    {
        var (guardianToken, groupUrl) = await CreateSharedGroupAsync();

        await PostScheduleAsync(guardianToken, groupUrl, Body(dosage: new string('d', 200)), 200);
        var response = await PostScheduleAsync(guardianToken, groupUrl, Body(dosage: new string('d', 201)), 400);

        MedicineTestHelpers.AssertValidationError(response, "Dosage");
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

    private static string ChildUrl(Guid childId) => $"/medicines/children/{childId}/schedules";

    private async Task<(string GuardianToken, string GroupUrl)> CreateSharedGroupAsync()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var groupId = await MedicineTestHelpers.ShareWithNewGroupAsync(fixture, guardianToken, child.Id);

        return (guardianToken, $"/medicines/groups/{groupId}/children/{child.Id}/schedules");
    }

    private static object Body(
        string name = "Amoxicillin",
        string dosage = "5 ml",
        TimeOnly[]? times = null,
        DateOnly? startDate = null,
        DateOnly? endDate = null) => new
        {
            Name = name,
            Dosage = dosage,
            Icon = "pill",
            Color = "#ff8800",
            Times = times ?? [new TimeOnly(8, 0)],
            StartDate = startDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = endDate
        };

    private Task<IScenarioResult> PostScheduleAsync(string token, string url, object body, int expectedStatus) =>
        fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(body).ToUrl(url);
            _.StatusCodeShouldBe(expectedStatus);
        });
}
