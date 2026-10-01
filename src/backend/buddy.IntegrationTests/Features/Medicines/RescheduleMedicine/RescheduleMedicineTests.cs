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
        var (guardianToken, url) = await CreateScheduleAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var response = await PatchScheduleAsync(guardianToken, url, [new TimeOnly(9, 0)], today, today.AddDays(-1), 400);

        MedicineTestHelpers.AssertValidationError(response, "EndDate");
    }

    [Fact]
    public async Task An_end_date_on_the_start_date_is_accepted()
    {
        var (guardianToken, url) = await CreateScheduleAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var response = await PatchScheduleAsync(guardianToken, url, [new TimeOnly(9, 0)], today, today, 200);

        Assert.Equal(today, response.ReadAsJson<MedicineScheduleDto>().EndDate);
    }

    [Fact]
    public async Task Rescheduling_to_no_dose_times_is_rejected()
    {
        var (guardianToken, url) = await CreateScheduleAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var response = await PatchScheduleAsync(guardianToken, url, [], today, null, 400);

        MedicineTestHelpers.AssertValidationError(response, "Times");
    }

    [Fact]
    public async Task A_group_reschedule_to_no_dose_times_is_rejected()
    {
        var (guardianToken, url) = await CreateScheduleAsync(viaGroup: true);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var response = await PatchScheduleAsync(guardianToken, url, [], today, null, 400);

        MedicineTestHelpers.AssertValidationError(response, "Times");
    }

    [Fact]
    public async Task A_group_reschedule_with_an_end_date_before_the_start_date_is_rejected_but_the_same_day_is_accepted()
    {
        var (guardianToken, url) = await CreateScheduleAsync(viaGroup: true);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await PatchScheduleAsync(guardianToken, url, [new TimeOnly(9, 0)], today, today, 200);
        var response = await PatchScheduleAsync(guardianToken, url, [new TimeOnly(9, 0)], today, today.AddDays(-1), 400);

        MedicineTestHelpers.AssertValidationError(response, "EndDate");
    }

    // Returns the schedule's reschedule URL, either the child-keyed route or (viaGroup) the
    // group-keyed one after sharing the child's medicine with a group the guardian owns.
    private async Task<(string GuardianToken, string Url)> CreateScheduleAsync(bool viaGroup = false)
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var schedule = await MedicineTestHelpers.CreateMedicineScheduleAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(schedule);

        if (!viaGroup)
        {
            return (guardianToken, $"/medicines/children/{child.Id}/schedules/{schedule.Id}/schedule");
        }

        var groupId = await MedicineTestHelpers.ShareWithNewGroupAsync(fixture, guardianToken, child.Id);
        return (guardianToken, $"/medicines/groups/{groupId}/children/{child.Id}/schedules/{schedule.Id}/schedule");
    }

    private Task<IScenarioResult> PatchScheduleAsync(
        string token, string url, TimeOnly[] times, DateOnly startDate, DateOnly? endDate, int expectedStatus) =>
        fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Times = times, StartDate = startDate, EndDate = endDate }).ToUrl(url);
            _.StatusCodeShouldBe(expectedStatus);
        });
}
