using Alba;

using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.WorkLocations.GetWorkLocationSchedule;

[Collection(BuddyApiCollection.Name)]
public sealed class GetWorkLocationScheduleTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("GetWorkLocationSchedule")]
    public async Task A_guardian_who_never_used_the_feature_reads_an_empty_one_week_schedule()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();

        var schedule = (await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"/work-locations/guardians/{guardianId}");
            _.StatusCodeShouldBeOk();
        })).ReadAsJson<WorkLocationScheduleDto>();

        Assert.Equal(guardianId, schedule.GuardianId);
        Assert.Empty(schedule.Locations);
        Assert.Equal(1, schedule.Pattern.CycleWeeks);
        Assert.Equal(DayOfWeek.Monday, schedule.Pattern.AnchorMonday.DayOfWeek);
        Assert.Empty(schedule.Pattern.Days);
    }

    [Fact]
    public async Task A_co_guardian_can_view_the_schedule()
    {
        var family = await WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);
        var stil = await WorkLocationTestHelpers.AddLocationAsync(fixture, family.FirstToken, "Stil");

        var schedule = (await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {family.SecondToken}");
            _.Get.Url($"/work-locations/guardians/{family.FirstId}");
            _.StatusCodeShouldBeOk();
        })).ReadAsJson<WorkLocationScheduleDto>();

        Assert.Equal(stil.Id, Assert.Single(schedule.Locations).Id);
    }

    [Fact]
    public async Task A_guardian_sharing_no_child_gets_not_found()
    {
        var (_, ownerToken, ownerId) = await fixture.CreateAuthenticatedUserAsync();
        await GuardianTestHelpers.CreateChildAsync(fixture, ownerToken, "Alex");
        var (_, otherToken, _) = await fixture.CreateAuthenticatedUserAsync();
        await GuardianTestHelpers.CreateChildAsync(fixture, otherToken, "Sam");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {otherToken}");
            _.Get.Url($"/work-locations/guardians/{ownerId}");
            _.StatusCodeShouldBe(404);
        });
    }

    [Fact]
    public async Task The_guardians_own_child_gets_not_found()
    {
        var (_, guardianToken, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        await WorkLocationTestHelpers.AddLocationAsync(fixture, guardianToken, "Stil");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Get.Url($"/work-locations/guardians/{guardianId}");
            _.StatusCodeShouldBe(404);
        });
    }
}
