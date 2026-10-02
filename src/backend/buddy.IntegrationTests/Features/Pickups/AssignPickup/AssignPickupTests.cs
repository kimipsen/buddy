using Alba;

using buddy.Common;
using buddy.Features.Pickups;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

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
            _.Put.Json(new { Assignee = new { Kind = PickupAssigneeKind.Guardian, GuardianId = guardianId }, Notes = "Bring an umbrella" })
                .ToUrl($"/pickups/children/{child.Id}/assignments")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "DropOff");
            _.StatusCodeShouldBeOk();
        });

        var occurrence = response.ReadAsJson<PickupOccurrenceDto>();
        Assert.Equal(today, occurrence.Date);
        Assert.Equal(PickupSlot.DropOff, occurrence.Slot);
        Assert.Equal(PickupAssigneeKind.Guardian, occurrence.Assignee.Kind);
        Assert.Equal(guardianId, occurrence.Assignee.GuardianId);
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
            _.Put.Json(new { Assignee = new { Kind = PickupAssigneeKind.SelfEscort } })
                .ToUrl($"/pickups/children/{child.Id}/assignments")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "PickUp");
            _.StatusCodeShouldBeOk();
        });

        Assert.Equal(PickupAssigneeKind.SelfEscort, response.ReadAsJson<PickupOccurrenceDto>().Assignee.Kind);
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
            _.Put.Json(new { Assignee = new { Kind = PickupAssigneeKind.Sibling, SiblingChildId = alice.Id } })
                .ToUrl($"/pickups/children/{bob.Id}/assignments")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "PickUp");
            _.StatusCodeShouldBeOk();
        });

        var occurrence = response.ReadAsJson<PickupOccurrenceDto>();
        Assert.Equal(PickupAssigneeKind.Sibling, occurrence.Assignee.Kind);
        Assert.Equal(alice.Id, occurrence.Assignee.SiblingChildId);
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
            _.Put.Json(new { Assignee = new { Kind = PickupAssigneeKind.Playdate, HostName = "Mia's mom", Location = "Mia's house" } })
                .ToUrl($"/pickups/children/{child.Id}/assignments")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "PickUp");
            _.StatusCodeShouldBeOk();
        });

        var occurrence = response.ReadAsJson<PickupOccurrenceDto>();
        Assert.Equal(PickupAssigneeKind.Playdate, occurrence.Assignee.Kind);
        Assert.Equal("Mia's mom", occurrence.Assignee.HostName);
        Assert.Equal("Mia's house", occurrence.Assignee.Location);
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
            _.Put.Json(new { Assignee = new { Kind = PickupAssigneeKind.SelfEscort } })
                .ToUrl($"/pickups/children/{child.Id}/assignments")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "PickUp");
            _.StatusCodeShouldBeOk();
        });

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { Assignee = new { Kind = PickupAssigneeKind.Playdate, HostName = "Mia's mom" } })
                .ToUrl($"/pickups/children/{child.Id}/assignments")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "PickUp");
            _.StatusCodeShouldBeOk();
        });

        Assert.Equal(PickupAssigneeKind.Playdate, response.ReadAsJson<PickupOccurrenceDto>().Assignee.Kind);

        var listResponse = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/pickups/children/{child.Id}/schedule?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}");
            _.StatusCodeShouldBeOk();
        });

        var occurrence = Assert.Single(listResponse.ReadAsJson<List<PickupOccurrenceDto>>());
        Assert.Equal(PickupAssigneeKind.Playdate, occurrence.Assignee.Kind);
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
            _.Put.Json(new { Assignee = new { Kind = PickupAssigneeKind.Guardian, GuardianId = unrelatedUserId } })
                .ToUrl($"/pickups/children/{child.Id}/assignments")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "DropOff");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Equal(["guardianId is not an active guardian of this child."], error.Details[""]);
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
            _.Put.Json(new { Assignee = new { Kind = PickupAssigneeKind.Sibling, SiblingChildId = unrelatedChild.Id } })
                .ToUrl($"/pickups/children/{child.Id}/assignments")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "PickUp");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Equal(["siblingChildId does not share an active guardian with this child."], error.Details[""]);
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
            _.Put.Json(new { Assignee = new { Kind = PickupAssigneeKind.SelfEscort } })
                .ToUrl($"/pickups/children/{child.Id}/assignments")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "PickUp");
            _.StatusCodeShouldBe(403);
        });
    }

    [Fact]
    public async Task Re_assigning_an_identical_playdate_appends_no_event()
    {
        var (guardianToken, childId) = await CreateChildAsync();
        var body = new { Assignee = new { Kind = PickupAssigneeKind.Playdate, HostName = "Mia's mom", Location = "Mia's house" }, Notes = "Snacks" };

        await AssignAsync(guardianToken, childId, body, 200);
        await AssignAsync(guardianToken, childId, body, 200);

        // PickupAssignee is a union of records, so the handler's before == after check compares the
        // case's values, not references: the second identical write is a no-op.
        var store = fixture.Host.Services.GetRequiredService<IPickupScheduleEventStore>();
        var scheduleId = await store.FindIdForChildAsync(new UserId(childId), CancellationToken.None);
        Assert.NotNull(scheduleId);
        var events = await store.ReadAsync(scheduleId, CancellationToken.None);
        Assert.Equal(["PickupScheduleCreated", "PickupAssigned"], events.Select(e => e.EventType));
    }

    [Fact]
    public async Task An_unknown_assignee_kind_is_rejected()
    {
        var (guardianToken, childId) = await CreateChildAsync();

        var response = await AssignAsync(guardianToken, childId, new { Assignee = new { Kind = 9 } }, 400);

        Assert.Equal("validation_error", response.ReadAsJson<ErrorEnvelope>().Code);
    }

    [Fact]
    public async Task An_assignee_without_a_kind_is_rejected()
    {
        var (guardianToken, childId) = await CreateChildAsync();

        var response = await AssignAsync(guardianToken, childId, new { Assignee = new { GuardianId = Guid.NewGuid() } }, 400);

        Assert.Equal("validation_error", response.ReadAsJson<ErrorEnvelope>().Code);
    }

    [Fact]
    public async Task The_assignee_kind_may_come_after_its_fields()
    {
        var (guardianToken, childId) = await CreateChildAsync();

        // PickupAssigneeDtoJsonConverter reads the whole object, so "kind" need not come first.
        var response = await AssignAsync(guardianToken, childId, new { Assignee = new { HostName = "Mia's mom", Kind = PickupAssigneeKind.Playdate } }, 200);

        var assignee = response.ReadAsJson<PickupOccurrenceDto>().Assignee;
        Assert.Equal(PickupAssigneeKind.Playdate, assignee.Kind);
        Assert.Equal("", assignee.Location);
    }

    [Fact]
    public async Task A_guardian_assignee_without_a_guardian_id_is_rejected()
    {
        var (guardianToken, childId) = await CreateChildAsync();

        var response = await AssignAsync(guardianToken, childId, new { Assignee = new { Kind = PickupAssigneeKind.Guardian } }, 400);

        AssertValidationError(response, "assignee.guardianId");
    }

    [Fact]
    public async Task A_sibling_assignee_without_a_sibling_child_id_is_rejected()
    {
        var (guardianToken, childId) = await CreateChildAsync();

        var response = await AssignAsync(guardianToken, childId, new { Assignee = new { Kind = PickupAssigneeKind.Sibling } }, 400);

        AssertValidationError(response, "assignee.siblingChildId");
    }

    [Fact]
    public async Task A_playdate_assignee_without_a_host_name_is_rejected()
    {
        var (guardianToken, childId) = await CreateChildAsync();

        var response = await AssignAsync(guardianToken, childId, new { Assignee = new { Kind = PickupAssigneeKind.Playdate, HostName = "" } }, 400);

        AssertValidationError(response, "Assignee.HostName");
    }

    [Fact]
    public async Task A_playdate_host_name_of_200_characters_is_accepted_but_201_is_rejected()
    {
        var (guardianToken, childId) = await CreateChildAsync();

        await AssignAsync(guardianToken, childId, new { Assignee = new { Kind = PickupAssigneeKind.Playdate, HostName = new string('h', 200) } }, 200);
        var response = await AssignAsync(guardianToken, childId, new { Assignee = new { Kind = PickupAssigneeKind.Playdate, HostName = new string('h', 201) } }, 400);

        AssertValidationError(response, "Assignee.HostName");
    }

    [Fact]
    public async Task The_host_name_rules_only_apply_to_a_playdate_assignee()
    {
        var (guardianToken, childId) = await CreateChildAsync();

        await AssignAsync(guardianToken, childId, new { Assignee = new { Kind = PickupAssigneeKind.SelfEscort, HostName = new string('h', 201) } }, 200);
    }

    [Fact]
    public async Task A_playdate_location_of_200_characters_is_accepted_but_201_is_rejected()
    {
        var (guardianToken, childId) = await CreateChildAsync();

        await AssignAsync(guardianToken, childId, new { Assignee = new { Kind = PickupAssigneeKind.Playdate, HostName = "Mia's mom", Location = new string('l', 200) } }, 200);
        var response = await AssignAsync(guardianToken, childId, new { Assignee = new { Kind = PickupAssigneeKind.Playdate, HostName = "Mia's mom", Location = new string('l', 201) } }, 400);

        AssertValidationError(response, "Assignee.Location");
    }

    [Fact]
    public async Task Playdate_contact_info_of_2000_characters_is_accepted_but_2001_is_rejected()
    {
        var (guardianToken, childId) = await CreateChildAsync();

        await AssignAsync(guardianToken, childId, new { Assignee = new { Kind = PickupAssigneeKind.Playdate, HostName = "Mia's mom", ContactInfo = new string('c', 2000) } }, 200);
        var response = await AssignAsync(guardianToken, childId, new { Assignee = new { Kind = PickupAssigneeKind.Playdate, HostName = "Mia's mom", ContactInfo = new string('c', 2001) } }, 400);

        AssertValidationError(response, "Assignee.ContactInfo");
    }

    [Fact]
    public async Task Notes_of_2000_characters_are_accepted_but_2001_are_rejected()
    {
        var (guardianToken, childId) = await CreateChildAsync();

        await AssignAsync(guardianToken, childId, new { Assignee = new { Kind = PickupAssigneeKind.SelfEscort }, Notes = new string('n', 2000) }, 200);
        var response = await AssignAsync(guardianToken, childId, new { Assignee = new { Kind = PickupAssigneeKind.SelfEscort }, Notes = new string('n', 2001) }, 400);

        AssertValidationError(response, "Notes");
    }

    private async Task<(string GuardianToken, Guid ChildId)> CreateChildAsync()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        return (guardianToken, child.Id);
    }

    private Task<IScenarioResult> AssignAsync(string token, Guid childId, object body, int expectedStatus) =>
        fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Json(body)
                .ToUrl($"/pickups/children/{childId}/assignments")
                .QueryString("date", $"{DateOnly.FromDateTime(DateTime.UtcNow):yyyy-MM-dd}")
                .QueryString("slot", "PickUp");
            _.StatusCodeShouldBe(expectedStatus);
        });

    private static void AssertValidationError(IScenarioResult response, string field)
    {
        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Contains(field, error.Details.Keys);
    }
}
