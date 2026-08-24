using Alba;

using buddy.Features.Groups;
using buddy.Features.Medicines;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.Medicines;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Medicines.ShareMedicineWithGroup;

[Collection(BuddyApiCollection.Name)]
public sealed class MedicineGroupSharingTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ShareMedicineWithGroup")]
    [CoversEndpoint("GetSharedMedicineGroup")]
    [CoversEndpoint("ListMedicineSchedulesForGroup")]
    [CoversEndpoint("CreateMedicineScheduleForGroup")]
    [CoversEndpoint("UpdateMedicineDetailsForGroup")]
    [CoversEndpoint("RescheduleMedicineForGroup")]
    [CoversEndpoint("ListTodaysDosesForGroup")]
    [CoversEndpoint("SetDoseStatusForGroup")]
    [CoversEndpoint("StopMedicineScheduleForGroup")]
    [CoversEndpoint("UnshareMedicineFromGroup")]
    public async Task Sharing_with_a_group_grants_its_manage_tier_members_full_access_until_unshared()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var amoxicillin = await MedicineTestHelpers.CreateMedicineScheduleAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(amoxicillin);

        // The guardian is the group's Owner, so ShareMedicineWithGroup's group-management check
        // is satisfied by the same call that created the group.
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, guardianToken, "Co-parents");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Url($"/medicines/children/{child.Id}/group-share/{groupId}");
            _.StatusCodeShouldBe(204);
        });

        var sharedGroupResponse = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/medicines/children/{child.Id}/group-share");
            _.StatusCodeShouldBeOk();
        });
        var sharedGroupDto = sharedGroupResponse.ReadAsJson<SharedMedicineGroupResponseDto>();
        Assert.Equal(groupId, sharedGroupDto.GroupId);
        Assert.Equal("Co-parents", sharedGroupDto.GroupName);

        // A default-policy Admin gets Manage tier the instant they're granted the role -- no
        // separate opt-in needed.
        var (_, adminToken, adminId) = await fixture.CreateAuthenticatedUserAsync();
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { Role = GroupRole.Admin }).ToUrl($"/groups/{groupId}/members/{adminId}");
            _.StatusCodeShouldBe(204);
        });

        var listResponse = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {adminToken}");
            _.Get.Url($"/medicines/groups/{groupId}/children/{child.Id}/schedules");
            _.StatusCodeShouldBeOk();
        });
        var listed = Assert.Single(listResponse.ReadAsJson<List<MedicineScheduleDto>>());
        Assert.Equal(amoxicillin.Id, listed.Id);

        var ibuprofen = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {adminToken}");
            _.Post.Json(new
            {
                Name = "Ibuprofen",
                Dosage = "5 ml",
                Icon = "pill",
                Color = "#ff0000",
                Times = new[] { new TimeOnly(12, 0) },
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
                EndDate = (DateOnly?)null
            }).ToUrl($"/medicines/groups/{groupId}/children/{child.Id}/schedules");
            _.StatusCodeShouldBeOk();
        });
        var ibuprofenSchedule = ibuprofen.ReadAsJson<MedicineScheduleDto>();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {adminToken}");
            _.Patch.Json(new { Name = "Ibuprofen (children's)", Dosage = "10 ml", Icon = "pill", Color = "#ff0000" })
                .ToUrl($"/medicines/groups/{groupId}/children/{child.Id}/schedules/{ibuprofenSchedule.Id}/details");
            _.StatusCodeShouldBeOk();
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {adminToken}");
            _.Patch.Json(new { Times = new[] { new TimeOnly(9, 0), new TimeOnly(21, 0) }, StartDate = DateOnly.FromDateTime(DateTime.UtcNow), EndDate = (DateOnly?)null })
                .ToUrl($"/medicines/groups/{groupId}/children/{child.Id}/schedules/{ibuprofenSchedule.Id}/schedule");
            _.StatusCodeShouldBeOk();
        });

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {adminToken}");
            _.Get.Url($"/medicines/groups/{groupId}/children/{child.Id}/doses")
                .QueryString("from", $"{today:yyyy-MM-dd}")
                .QueryString("to", $"{today:yyyy-MM-dd}");
            _.StatusCodeShouldBeOk();
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {adminToken}");
            _.Put.Json(new { Status = DoseStatus.Taken })
                .ToUrl($"/medicines/groups/{groupId}/children/{child.Id}/doses/{ibuprofenSchedule.Id}")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("time", "09:00:00");
            _.StatusCodeShouldBeOk();
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {adminToken}");
            _.Delete.Url($"/medicines/groups/{groupId}/children/{child.Id}/schedules/{ibuprofenSchedule.Id}");
            _.StatusCodeShouldBe(204);
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Delete.Url($"/medicines/children/{child.Id}/group-share/{groupId}");
            _.StatusCodeShouldBe(204);
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {adminToken}");
            _.Get.Url($"/medicines/groups/{groupId}/children/{child.Id}/schedules");
            _.StatusCodeShouldBe(404);
        });

        var afterUnshare = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/medicines/children/{child.Id}/group-share");
            _.StatusCodeShouldBeOk();
        });
        Assert.Null(afterUnshare.ReadAsJson<SharedMedicineGroupResponseDto>().GroupId);
    }

    [Fact]
    public async Task A_member_with_the_default_none_policy_cannot_reach_the_shared_schedules()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        await MedicineTestHelpers.CreateMedicineScheduleAsync(fixture, guardianToken, child.Id);

        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, guardianToken, "Co-parents");
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Url($"/medicines/children/{child.Id}/group-share/{groupId}");
            _.StatusCodeShouldBe(204);
        });

        var (_, memberToken, memberId) = await fixture.CreateAuthenticatedUserAsync();
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { Role = GroupRole.Member }).ToUrl($"/groups/{groupId}/members/{memberId}");
            _.StatusCodeShouldBe(204);
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {memberToken}");
            _.Get.Url($"/medicines/groups/{groupId}/children/{child.Id}/schedules");
            _.StatusCodeShouldBe(404);
        });
    }

    [Fact]
    public async Task Sharing_requires_the_guardian_to_also_manage_the_target_group()
    {
        var (_, guardianToken, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        var (_, otherOwnerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, otherOwnerToken, "Not the guardian's group");
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {otherOwnerToken}");
            _.Put.Json(new { Role = GroupRole.Member }).ToUrl($"/groups/{groupId}/members/{guardianId}");
            _.StatusCodeShouldBe(204);
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Url($"/medicines/children/{child.Id}/group-share/{groupId}");
            _.StatusCodeShouldBe(403);
        });
    }
}
