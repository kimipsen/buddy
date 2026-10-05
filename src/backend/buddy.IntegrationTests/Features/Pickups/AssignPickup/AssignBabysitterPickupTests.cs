using Alba;

using buddy.Features.Pickups;
using buddy.IntegrationTests.Features.Babysitters;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.WorkLocations;
using buddy.IntegrationTests.Fixtures;

using Xunit;

namespace buddy.IntegrationTests.Features.Pickups.AssignPickup;

// The Babysitter assignee case (docs/backend/analysis/babysitters.md, Question 4).
[Collection(BuddyApiCollection.Name)]
public sealed class AssignBabysitterPickupTests(BuddyApiFixture fixture)
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task A_guardian_can_assign_their_own_babysitter_and_the_response_carries_the_name()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        var anna = await BabysitterTestHelpers.AddAsync(fixture, token, "Anna");

        var occurrence = await BabysitterTestHelpers.AssignAsync(fixture, token, child.Id, Today, guardianId, anna.Id);

        Assert.NotNull(occurrence);
        Assert.Equal(PickupAssigneeKind.Babysitter, occurrence.Assignee.Kind);
        Assert.Equal(guardianId, occurrence.Assignee.GuardianId);
        Assert.Equal(anna.Id, occurrence.Assignee.BabysitterId);
        Assert.Equal("Anna", occurrence.Assignee.Name);

        var listed = Assert.Single(await BabysitterTestHelpers.ListScheduleAsync(fixture, token, child.Id, Today));
        Assert.Equal(occurrence, listed);
    }

    [Fact]
    public async Task A_guardian_can_assign_a_co_guardians_babysitter()
    {
        var family = await WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);
        var anna = await BabysitterTestHelpers.AddAsync(fixture, family.SecondToken, "Anna");

        var occurrence = await BabysitterTestHelpers.AssignAsync(fixture, family.FirstToken, family.Child.Id, Today, family.SecondId, anna.Id);

        Assert.Equal("Anna", occurrence?.Assignee.Name);
    }

    [Fact]
    public async Task A_name_sent_in_the_request_is_ignored()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        var anna = await BabysitterTestHelpers.AddAsync(fixture, token, "Anna");

        var occurrence = (await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Json(new { Assignee = new { Kind = PickupAssigneeKind.Babysitter, GuardianId = guardianId, BabysitterId = anna.Id, Name = "Mallory" } })
                .ToUrl($"/pickups/children/{child.Id}/assignments")
                .QueryString("date", $"{Today:yyyy-MM-dd}")
                .QueryString("slot", "PickUp");
            _.StatusCodeShouldBeOk();
        })).ReadAsJson<PickupOccurrenceDto>();

        Assert.Equal("Anna", occurrence.Assignee.Name);
    }

    [Fact]
    public async Task Renaming_a_babysitter_changes_the_name_on_the_schedule_and_archiving_keeps_it()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        var anna = await BabysitterTestHelpers.AddAsync(fixture, token, "Anna");
        await BabysitterTestHelpers.AssignAsync(fixture, token, child.Id, Today, guardianId, anna.Id);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Name = "Anne" }).ToUrl($"/babysitters/me/{anna.Id}");
            _.StatusCodeShouldBeOk();
        });

        Assert.Equal("Anne", Assert.Single(await BabysitterTestHelpers.ListScheduleAsync(fixture, token, child.Id, Today)).Assignee.Name);

        await BabysitterTestHelpers.ArchiveAsync(fixture, token, anna.Id);

        Assert.Equal("Anne", Assert.Single(await BabysitterTestHelpers.ListScheduleAsync(fixture, token, child.Id, Today)).Assignee.Name);
    }

    [Fact]
    public async Task A_slot_keeps_the_name_after_the_owning_guardians_link_is_revoked()
    {
        var family = await WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);
        var anna = await BabysitterTestHelpers.AddAsync(fixture, family.SecondToken, "Anna");
        await BabysitterTestHelpers.AssignAsync(fixture, family.FirstToken, family.Child.Id, Today, family.SecondId, anna.Id);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {family.SecondToken}");
            _.Delete.Url($"/users/me/children/{family.Child.Id}/guardian-link");
            _.StatusCodeShouldBe(204);
        });

        var assignee = Assert.Single(await BabysitterTestHelpers.ListScheduleAsync(fixture, family.FirstToken, family.Child.Id, Today)).Assignee;
        Assert.Equal("Anna", assignee.Name);
    }

    [Fact]
    public async Task The_child_sees_the_babysitters_name_but_not_the_contact_info()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        var anna = await BabysitterTestHelpers.AddAsync(fixture, token, "Anna", "+45 12 34 56 78");
        await BabysitterTestHelpers.AssignAsync(fixture, token, child.Id, Today, guardianId, anna.Id);
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        var assignee = Assert.Single(await BabysitterTestHelpers.ListScheduleAsync(fixture, childToken, child.Id, Today)).Assignee;

        Assert.Equal("Anna", assignee.Name);
        Assert.Null(assignee.ContactInfo);
    }

    [Fact]
    public async Task An_archived_or_unknown_babysitter_is_rejected()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        var anna = await BabysitterTestHelpers.AddAsync(fixture, token, "Anna");
        await BabysitterTestHelpers.ArchiveAsync(fixture, token, anna.Id);

        await BabysitterTestHelpers.AssignAsync(fixture, token, child.Id, Today, guardianId, anna.Id, expectedStatus: 400);
        await BabysitterTestHelpers.AssignAsync(fixture, token, child.Id, Today, guardianId, Guid.CreateVersion7(), expectedStatus: 400);
    }

    [Fact]
    public async Task A_babysitter_of_someone_who_is_not_the_childs_guardian_is_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        var (_, strangerToken, strangerId) = await fixture.CreateAuthenticatedUserAsync();
        var anna = await BabysitterTestHelpers.AddAsync(fixture, strangerToken, "Anna");

        await BabysitterTestHelpers.AssignAsync(fixture, token, child.Id, Today, strangerId, anna.Id, expectedStatus: 400);
    }

    [Fact]
    public async Task A_babysitter_id_paired_with_the_wrong_guardian_is_rejected()
    {
        var family = await WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);
        var anna = await BabysitterTestHelpers.AddAsync(fixture, family.SecondToken, "Anna");

        await BabysitterTestHelpers.AssignAsync(fixture, family.FirstToken, family.Child.Id, Today, family.FirstId, anna.Id, expectedStatus: 400);
    }
}
