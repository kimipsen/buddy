using Alba;

using buddy.Features.Pickups;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Pickups.AssignPickup;

[Collection(BuddyApiCollection.Name)]
public sealed class AssignPickupTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("AssignPickup")]
    public async Task A_guardian_can_assign_another_guardian_to_a_slot()
    {
        var (_, guardianToken, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { Kind = PickupAssigneeKind.Guardian, GuardianId = guardianId, Notes = "Bring an umbrella" })
                .ToUrl($"/pickups/children/{child.Id}/assignments")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "DropOff");
            _.StatusCodeShouldBeOk();
        });

        var occurrence = response.ReadAsJson<PickupOccurrenceDto>();
        Assert.Equal(today, occurrence.Date);
        Assert.Equal(PickupSlot.DropOff, occurrence.Slot);
        Assert.Equal(PickupAssigneeKind.Guardian, occurrence.Kind);
        Assert.Equal(guardianId, occurrence.GuardianId);
        Assert.Equal("Bring an umbrella", occurrence.Notes);
    }

    [Fact]
    public async Task A_guardian_can_assign_self_escort()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { Kind = PickupAssigneeKind.SelfEscort })
                .ToUrl($"/pickups/children/{child.Id}/assignments")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "PickUp");
            _.StatusCodeShouldBeOk();
        });

        Assert.Equal(PickupAssigneeKind.SelfEscort, response.ReadAsJson<PickupOccurrenceDto>().Kind);
    }

    [Fact]
    public async Task A_guardian_can_assign_a_sibling_as_escort()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var alice = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alice");
        var bob = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Bob");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { Kind = PickupAssigneeKind.Sibling, SiblingChildId = alice.Id })
                .ToUrl($"/pickups/children/{bob.Id}/assignments")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "PickUp");
            _.StatusCodeShouldBeOk();
        });

        var occurrence = response.ReadAsJson<PickupOccurrenceDto>();
        Assert.Equal(PickupAssigneeKind.Sibling, occurrence.Kind);
        Assert.Equal(alice.Id, occurrence.SiblingChildId);
    }

    [Fact]
    public async Task A_guardian_can_assign_a_playdate()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { Kind = PickupAssigneeKind.Playdate, PlaydateHostName = "Mia's mom", PlaydateLocation = "Mia's house" })
                .ToUrl($"/pickups/children/{child.Id}/assignments")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "PickUp");
            _.StatusCodeShouldBeOk();
        });

        var occurrence = response.ReadAsJson<PickupOccurrenceDto>();
        Assert.Equal(PickupAssigneeKind.Playdate, occurrence.Kind);
        Assert.Equal("Mia's mom", occurrence.PlaydateHostName);
        Assert.Equal("Mia's house", occurrence.PlaydateLocation);
    }

    [Fact]
    public async Task Reassigning_the_same_slot_overwrites_the_previous_assignment()
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

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { Kind = PickupAssigneeKind.Playdate, PlaydateHostName = "Mia's mom" })
                .ToUrl($"/pickups/children/{child.Id}/assignments")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "PickUp");
            _.StatusCodeShouldBeOk();
        });

        Assert.Equal(PickupAssigneeKind.Playdate, response.ReadAsJson<PickupOccurrenceDto>().Kind);

        var listResponse = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/pickups/children/{child.Id}/schedule?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}");
            _.StatusCodeShouldBeOk();
        });

        var occurrence = Assert.Single(listResponse.ReadAsJson<List<PickupOccurrenceDto>>());
        Assert.Equal(PickupAssigneeKind.Playdate, occurrence.Kind);
    }

    [Fact]
    public async Task Assigning_a_guardian_who_is_not_an_active_guardian_of_the_child_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var (_, _, unrelatedUserId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { Kind = PickupAssigneeKind.Guardian, GuardianId = unrelatedUserId })
                .ToUrl($"/pickups/children/{child.Id}/assignments")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "DropOff");
            _.StatusCodeShouldBe(400);
        });

        Assert.Equal("guardianId is not an active guardian of this child.", response.ReadAsJson<string>());
    }

    [Fact]
    public async Task Assigning_an_unrelated_child_as_sibling_escort_is_rejected()
    {
        var (_, firstGuardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var (_, secondGuardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, firstGuardianToken, "Alex");
        var unrelatedChild = await GuardianTestHelpers.CreateChildAsync(fixture, secondGuardianToken, "Sam");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {firstGuardianToken}");
            _.Put.Json(new { Kind = PickupAssigneeKind.Sibling, SiblingChildId = unrelatedChild.Id })
                .ToUrl($"/pickups/children/{child.Id}/assignments")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "PickUp");
            _.StatusCodeShouldBe(400);
        });

        Assert.Equal("siblingChildId does not share an active guardian with this child.", response.ReadAsJson<string>());
    }

    [Fact]
    public async Task The_child_cannot_assign_a_slot()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Put.Json(new { Kind = PickupAssigneeKind.SelfEscort })
                .ToUrl($"/pickups/children/{child.Id}/assignments")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "PickUp");
            _.StatusCodeShouldBe(403);
        });
    }
}
