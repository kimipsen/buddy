using Alba;

using buddy.Features.Medicines;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.Medicines;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Medicines.ListTodaysDoses;

[Collection(BuddyApiCollection.Name)]
public sealed class ListTodaysDosesTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ListTodaysDoses")]
    public async Task The_child_can_see_todays_doses_all_pending()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await MedicineTestHelpers.CreateMedicineScheduleAsync(
            fixture, guardianToken, child.Id,
            new CreateMedicineScheduleOptions(Times: [new TimeOnly(8, 0), new TimeOnly(20, 0)], StartDate: today));

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Get.Url($"/medicines/children/{child.Id}/doses?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}");
            _.StatusCodeShouldBeOk();
        });

        var doses = response.ReadAsJson<List<MedicineDoseOccurrenceDto>>();
        Assert.Equal(2, doses.Count);
        Assert.All(doses, d => Assert.Equal(DoseStatus.Pending, d.Status));
    }

    [Fact]
    public async Task Doses_before_the_start_date_or_after_the_end_date_are_excluded()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await MedicineTestHelpers.CreateMedicineScheduleAsync(
            fixture, guardianToken, child.Id,
            new CreateMedicineScheduleOptions(Times: [new TimeOnly(8, 0)], StartDate: today.AddDays(2), EndDate: today.AddDays(3)));

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/medicines/children/{child.Id}/doses?from={today:yyyy-MM-dd}&to={today.AddDays(1):yyyy-MM-dd}");
            _.StatusCodeShouldBeOk();
        });

        Assert.Empty(response.ReadAsJson<List<MedicineDoseOccurrenceDto>>());
    }

    [Fact]
    public async Task A_third_party_with_no_guardian_link_gets_not_found()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {strangerToken}");
            _.Get.Url($"/medicines/children/{child.Id}/doses?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}");
            _.StatusCodeShouldBe(404);
        });
    }

    [Fact]
    public async Task Rejects_a_range_where_to_is_before_from()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/medicines/children/{child.Id}/doses?from={today:yyyy-MM-dd}&to={today.AddDays(-1):yyyy-MM-dd}");
            _.StatusCodeShouldBe(400);
        });
    }
}
