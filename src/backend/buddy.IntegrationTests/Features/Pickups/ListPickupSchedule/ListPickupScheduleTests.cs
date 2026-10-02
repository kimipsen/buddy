using Alba;

using buddy.Common;
using buddy.Features.Pickups;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Pickups.ListPickupSchedule;

[Collection(BuddyApiCollection.Name)]
public sealed class ListPickupScheduleTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ListPickupSchedule")]
    public async Task The_child_can_view_their_own_schedule()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { Assignee = new { Kind = PickupAssigneeKind.SelfEscort } })
                .ToUrl($"/pickups/children/{child.Id}/assignments")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "PickUp");
            _.StatusCodeShouldBeOk();
        });

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Get.Url($"/pickups/children/{child.Id}/schedule?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}");
            _.StatusCodeShouldBeOk();
        });

        var occurrence = Assert.Single(response.ReadAsJson<List<PickupOccurrenceDto>>());
        Assert.Equal(PickupAssigneeKind.SelfEscort, occurrence.Assignee.Kind);
    }

    [Fact]
    public async Task An_unplanned_slot_is_simply_absent_from_the_list()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/pickups/children/{child.Id}/schedule?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}");
            _.StatusCodeShouldBeOk();
        });

        Assert.Empty(response.ReadAsJson<List<PickupOccurrenceDto>>());
    }

    [Fact]
    public async Task An_unrelated_user_gets_not_found()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var (_, unrelatedToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {unrelatedToken}");
            _.Get.Url($"/pickups/children/{child.Id}/schedule?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}");
            _.StatusCodeShouldBe(404);
        });
    }

    [Fact]
    public async Task A_range_with_to_before_from_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var response = await GetScheduleAsync(guardianToken, child.Id, today, today.AddDays(-1), 400);

        AssertValidationError(response, "To");
    }

    [Fact]
    public async Task A_range_of_31_days_is_accepted_but_32_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await GetScheduleAsync(guardianToken, child.Id, today, today.AddDays(31), 200);
        var response = await GetScheduleAsync(guardianToken, child.Id, today, today.AddDays(32), 400);

        AssertValidationError(response, "To");
    }

    private Task<IScenarioResult> GetScheduleAsync(string token, Guid childId, DateOnly from, DateOnly to, int expectedStatus) =>
        fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"/pickups/children/{childId}/schedule?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");
            _.StatusCodeShouldBe(expectedStatus);
        });

    private static void AssertValidationError(IScenarioResult response, string field)
    {
        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Contains(field, error.Details.Keys);
    }
}
