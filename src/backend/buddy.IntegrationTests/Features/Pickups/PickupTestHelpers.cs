using Alba;

using buddy.Features.Pickups;
using buddy.IntegrationTests.Fixtures;

namespace buddy.IntegrationTests.Features.Pickups;

internal static class PickupTestHelpers
{
    public static async Task<PickupIcalTokenResponseDto> CreateIcalTokenAsync(BuddyApiFixture fixture, string guardianToken)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Url("/pickups/me/ical-tokens");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<PickupIcalTokenResponseDto>();
    }

    public static async Task<IReadOnlyList<PickupIcalTokenSummaryDto>> ListIcalTokensAsync(BuddyApiFixture fixture, string guardianToken)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url("/pickups/me/ical-tokens");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<List<PickupIcalTokenSummaryDto>>();
    }

    public static Task AssignAsync(BuddyApiFixture fixture, string guardianToken, Guid childId, DateOnly date, PickupSlot slot, object assignee, TimeOnly? time = null, string? notes = null) =>
        fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { Assignee = assignee, Time = time, Notes = notes })
                .ToUrl($"/pickups/children/{childId}/assignments")
                .QueryString("date", date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture))
                .QueryString("slot", slot.ToString());
            _.StatusCodeShouldBeOk();
        });

    public static object Guardian(Guid guardianId) => new { Kind = (int)PickupAssigneeKind.Guardian, GuardianId = guardianId };

    public static object SelfEscort() => new { Kind = (int)PickupAssigneeKind.SelfEscort };

    public static object Playdate(string hostName) => new { Kind = (int)PickupAssigneeKind.Playdate, HostName = hostName };

    public static async Task<string> ReadFeedAsync(BuddyApiFixture fixture, string subscriptionPath)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.Get.Url(subscriptionPath);
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsText();
    }
}
