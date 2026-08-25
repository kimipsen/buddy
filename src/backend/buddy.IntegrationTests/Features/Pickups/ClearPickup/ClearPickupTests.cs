using Alba;

using buddy.Features.Pickups;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Pickups.ClearPickup;

[Collection(BuddyApiCollection.Name)]
public sealed class ClearPickupTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ClearPickup")]
    public async Task Clearing_a_slot_removes_it_from_the_schedule()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { Kind = PickupAssigneeKind.SelfEscort })
                .ToUrl($"/pickups/children/{child.Id}/assignments")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "PickUp");
            _.StatusCodeShouldBeOk();
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Delete.Url($"/pickups/children/{child.Id}/assignments")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "PickUp");
            _.StatusCodeShouldBe(204);
        });

        var listResponse = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/pickups/children/{child.Id}/schedule?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}");
            _.StatusCodeShouldBeOk();
        });

        Assert.Empty(listResponse.ReadAsJson<List<PickupOccurrenceDto>>());
    }

    [Fact]
    public async Task Clearing_an_already_empty_slot_is_idempotent()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Delete.Url($"/pickups/children/{child.Id}/assignments")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "DropOff");
            _.StatusCodeShouldBe(204);
        });
    }

    [Fact]
    public async Task The_child_cannot_clear_a_slot()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Delete.Url($"/pickups/children/{child.Id}/assignments")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "PickUp");
            _.StatusCodeShouldBe(403);
        });
    }
}
