using Alba;

using buddy.Features.Pickups;
using buddy.IntegrationTests.Features.Pickups;
using buddy.IntegrationTests.Fixtures;

namespace buddy.IntegrationTests.Features.Babysitters;

// Response shapes, matching BabysitterSummary / ChildBabysitter (Features/Babysitters).
internal sealed record BabysitterDto(Guid Id, string Name, string ContactInfo, bool IsArchived);

internal sealed record ChildBabysitterDto(Guid GuardianId, Guid Id, string Name, string ContactInfo);

internal static class BabysitterTestHelpers
{
    public static async Task<BabysitterDto> AddAsync(BuddyApiFixture fixture, string token, string name, string? contactInfo = null)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(new { Name = name, ContactInfo = contactInfo }).ToUrl("/babysitters/me");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<BabysitterDto>();
    }

    public static async Task ArchiveAsync(BuddyApiFixture fixture, string token, Guid babysitterId) =>
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url($"/babysitters/me/{babysitterId}");
            _.StatusCodeShouldBe(204);
        });

    public static async Task<BabysitterDto[]> ListMineAsync(BuddyApiFixture fixture, string token)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url("/babysitters/me");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<BabysitterDto[]>();
    }

    // PUTs a babysitter assignment and returns the response, or null when a non-200 status was expected.
    public static async Task<PickupOccurrenceDto?> AssignAsync(
        BuddyApiFixture fixture, string token, Guid childId, DateOnly date, Guid guardianId, Guid babysitterId, int expectedStatus = 200)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Json(new { Assignee = new { Kind = PickupAssigneeKind.Babysitter, GuardianId = guardianId, BabysitterId = babysitterId } })
                .ToUrl($"/pickups/children/{childId}/assignments")
                .QueryString("date", $"{date:yyyy-MM-dd}")
                .QueryString("slot", "PickUp");
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<PickupOccurrenceDto>() : null;
    }

    public static async Task<PickupOccurrenceDto[]> ListScheduleAsync(BuddyApiFixture fixture, string token, Guid childId, DateOnly date)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"/pickups/children/{childId}/schedule?from={date:yyyy-MM-dd}&to={date:yyyy-MM-dd}");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<PickupOccurrenceDto[]>();
    }
}
