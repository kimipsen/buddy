using Alba;

using buddy.IntegrationTests.Fixtures;

namespace buddy.IntegrationTests.Features.Feelings;

// Response shapes matching FeelingCheckInResponse / MyFeelingsTodayResponse / FeelingCheckInsResponse
// / FeelingsShareLinkResponse / SharedFeelingsResponse (Features/Feelings). Enums travel by name.
internal sealed record FeelingCheckInDto(
    Guid Id,
    string Zone,
    List<string> Tags,
    string Note,
    DateOnly Date,
    TimeOnly Time,
    Guid LoggedBy,
    bool IsSelfReport,
    DateTimeOffset RecordedAt);

internal sealed record MyFeelingsTodayDto(DateOnly Date, List<TimeOnly> PromptTimes, string DisplayMode, List<FeelingCheckInDto> CheckIns);

internal sealed record FeelingCheckInsDto(DateOnly From, DateOnly To, List<TimeOnly> PromptTimes, string DisplayMode, List<FeelingCheckInDto> CheckIns);

internal sealed record PromptTimesDto(List<TimeOnly> Times);

internal sealed record DisplayModeDto(string DisplayMode);

internal sealed record FeelingsShareLinkDto(Guid Id, string Token, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt);

internal sealed record FeelingsShareLinkSummaryDto(Guid Id, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt);

internal sealed record SharedFeelingsDto(
    string ChildGivenName,
    string ChildFamilyName,
    DateOnly From,
    DateOnly To,
    DateTimeOffset? ExpiresAt,
    List<FeelingCheckInDto> CheckIns);

internal static class FeelingsTestHelpers
{
    public static async Task<FeelingCheckInDto?> CheckInAsync(BuddyApiFixture fixture, string childToken, object body, int expectedStatus = 200) =>
        await CheckInAsync(fixture.Host, childToken, body, expectedStatus);

    public static async Task<FeelingCheckInDto?> CheckInAsync(IAlbaHost host, string childToken, object body, int expectedStatus = 200)
    {
        var response = await host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Post.Json(body).ToUrl("/feelings/me/check-ins");
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<FeelingCheckInDto>() : null;
    }

    public static async Task UndoAsync(BuddyApiFixture fixture, string childToken, Guid checkInId, int expectedStatus = 204) =>
        await UndoAsync(fixture.Host, childToken, checkInId, expectedStatus);

    public static async Task<IScenarioResult> UndoAsync(IAlbaHost host, string childToken, Guid checkInId, int expectedStatus = 204) =>
        await host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Delete.Url($"/feelings/me/check-ins/{checkInId}");
            _.StatusCodeShouldBe(expectedStatus);
        });

    public static async Task<MyFeelingsTodayDto?> GetTodayAsync(BuddyApiFixture fixture, string token, int expectedStatus = 200)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url("/feelings/me/today");
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<MyFeelingsTodayDto>() : null;
    }

    public static async Task<FeelingCheckInDto?> LogAsync(BuddyApiFixture fixture, string token, Guid childId, object body, int expectedStatus = 200)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(body).ToUrl($"/feelings/children/{childId}/check-ins");
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<FeelingCheckInDto>() : null;
    }

    public static async Task<FeelingCheckInDto?> EditAsync(BuddyApiFixture fixture, string token, Guid childId, Guid checkInId, object body, int expectedStatus = 200)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Json(body).ToUrl($"/feelings/children/{childId}/check-ins/{checkInId}");
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<FeelingCheckInDto>() : null;
    }

    public static async Task<IScenarioResult> EditRawAsync(BuddyApiFixture fixture, string token, Guid childId, Guid checkInId, object body, int expectedStatus) =>
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Json(body).ToUrl($"/feelings/children/{childId}/check-ins/{checkInId}");
            _.StatusCodeShouldBe(expectedStatus);
        });

    public static async Task RemoveAsync(BuddyApiFixture fixture, string token, Guid childId, Guid checkInId, int expectedStatus = 204) =>
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url($"/feelings/children/{childId}/check-ins/{checkInId}");
            _.StatusCodeShouldBe(expectedStatus);
        });

    public static async Task<FeelingCheckInsDto?> ListAsync(BuddyApiFixture fixture, string token, Guid childId, DateOnly from, DateOnly to, int expectedStatus = 200)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"/feelings/children/{childId}/check-ins?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<FeelingCheckInsDto>() : null;
    }

    public static async Task<PromptTimesDto?> SetPromptTimesAsync(BuddyApiFixture fixture, string token, Guid childId, string[] times, int expectedStatus = 200)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Json(new { Times = times }).ToUrl($"/feelings/children/{childId}/prompt-times");
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<PromptTimesDto>() : null;
    }

    public static async Task<DisplayModeDto?> SetDisplayModeAsync(BuddyApiFixture fixture, string token, Guid childId, object displayMode, int expectedStatus = 200)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Json(new { DisplayMode = displayMode }).ToUrl($"/feelings/children/{childId}/display-mode");
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<DisplayModeDto>() : null;
    }

    public static async Task<FeelingsShareLinkDto?> CreateShareLinkAsync(BuddyApiFixture fixture, string token, Guid childId, DateTimeOffset? expiresAt = null, int expectedStatus = 200)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(new { ExpiresAt = expiresAt }).ToUrl($"/feelings/children/{childId}/share-links");
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<FeelingsShareLinkDto>() : null;
    }

    public static async Task<List<FeelingsShareLinkSummaryDto>?> ListShareLinksAsync(BuddyApiFixture fixture, string token, Guid childId, int expectedStatus = 200)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"/feelings/children/{childId}/share-links");
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<List<FeelingsShareLinkSummaryDto>>() : null;
    }

    public static async Task RevokeShareLinkAsync(BuddyApiFixture fixture, string token, Guid childId, Guid shareLinkId, int expectedStatus = 204) =>
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url($"/feelings/children/{childId}/share-links/{shareLinkId}");
            _.StatusCodeShouldBe(expectedStatus);
        });

    // Deliberately no Authorization header -- the share link is the only credential.
    public static async Task<SharedFeelingsDto?> GetSharedAsync(BuddyApiFixture fixture, string shareToken, string query = "", int expectedStatus = 200)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.Get.Url($"/feelings/shared/{shareToken}{query}");
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<SharedFeelingsDto>() : null;
    }

    public static async Task SetChildTimeZoneAsync(BuddyApiFixture fixture, string guardianToken, Guid childId, string timeZoneId) =>
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Patch.Json(new { TimeZoneId = timeZoneId }).ToUrl($"/users/me/children/{childId}/timezone");
            _.StatusCodeShouldBe(200);
        });

    // "Now" in the given zone as the API sees it, to the minute.
    public static (DateOnly Date, TimeOnly Time) LocalNow(string timeZoneId)
    {
        var local = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));
        return (DateOnly.FromDateTime(local.DateTime), new TimeOnly(local.Hour, local.Minute));
    }
}
