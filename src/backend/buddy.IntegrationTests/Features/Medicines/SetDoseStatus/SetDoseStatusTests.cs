using Alba;

using buddy.Features.Medicines;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.Medicines;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Medicines.SetDoseStatus;

[Collection(BuddyApiCollection.Name)]
public sealed class SetDoseStatusTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("SetDoseStatus")]
    public async Task The_child_can_mark_their_own_dose_taken()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var doseTime = new TimeOnly(8, 0);
        var schedule = await MedicineTestHelpers.CreateMedicineScheduleAsync(
            fixture, guardianToken, child.Id, new CreateMedicineScheduleOptions(Times: [doseTime], StartDate: today));
        Assert.NotNull(schedule);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            // ToUrl() takes the path literally -- query parameters must be chained via
            // QueryString(...), not embedded as a "?..." suffix in the path string.
            _.Put.Json(new { Status = DoseStatus.Taken })
                .ToUrl($"/medicines/children/{child.Id}/doses/{schedule.Id}")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("time", $"{doseTime:HH:mm:ss}");
            _.StatusCodeShouldBeOk();
        });

        var updated = response.ReadAsJson<MedicineDoseOccurrenceDto>();
        Assert.Equal(DoseStatus.Taken, updated.Status);

        var listResponse = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Get.Url($"/medicines/children/{child.Id}/doses?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}");
            _.StatusCodeShouldBeOk();
        });

        var dose = Assert.Single(listResponse.ReadAsJson<List<MedicineDoseOccurrenceDto>>());
        Assert.Equal(DoseStatus.Taken, dose.Status);
    }

    [Fact]
    public async Task A_guardian_can_mark_a_dose_on_the_childs_behalf()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var doseTime = new TimeOnly(8, 0);
        var schedule = await MedicineTestHelpers.CreateMedicineScheduleAsync(
            fixture, guardianToken, child.Id, new CreateMedicineScheduleOptions(Times: [doseTime], StartDate: today));
        Assert.NotNull(schedule);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { Status = DoseStatus.Skipped })
                .ToUrl($"/medicines/children/{child.Id}/doses/{schedule.Id}")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("time", $"{doseTime:HH:mm:ss}");
            _.StatusCodeShouldBeOk();
        });
    }

    [Fact]
    public async Task A_time_that_isnt_part_of_the_schedule_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var schedule = await MedicineTestHelpers.CreateMedicineScheduleAsync(
            fixture, guardianToken, child.Id, new CreateMedicineScheduleOptions(Times: [new TimeOnly(8, 0)], StartDate: today));
        Assert.NotNull(schedule);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { Status = DoseStatus.Taken })
                .ToUrl($"/medicines/children/{child.Id}/doses/{schedule.Id}")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("time", "23:00:00");
            _.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task A_third_party_with_no_guardian_link_gets_not_found()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var doseTime = new TimeOnly(8, 0);
        var schedule = await MedicineTestHelpers.CreateMedicineScheduleAsync(
            fixture, guardianToken, child.Id, new CreateMedicineScheduleOptions(Times: [doseTime], StartDate: today));
        Assert.NotNull(schedule);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {strangerToken}");
            _.Put.Json(new { Status = DoseStatus.Taken })
                .ToUrl($"/medicines/children/{child.Id}/doses/{schedule.Id}")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("time", $"{doseTime:HH:mm:ss}");
            _.StatusCodeShouldBe(404);
        });
    }
}
