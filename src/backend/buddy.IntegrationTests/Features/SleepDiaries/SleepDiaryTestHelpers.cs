using buddy.IntegrationTests.Fixtures;

namespace buddy.IntegrationTests.Features.SleepDiaries;

// Response shapes matching SleepEntryResponse / SleepDiaryEntriesResponse / SleepDiaryShareLinkResponse
// / SharedSleepDiaryResponse (Features/SleepDiaries).
internal sealed record SleepIntervalDto(TimeOnly StartTime, int DurationMinutes);

internal sealed record SleepEntryDto(
    DateOnly Date,
    bool IsWeekend,
    TimeOnly? RoutineStartTime,
    TimeOnly? RitualStartTime,
    TimeOnly? RitualEndTime,
    TimeOnly? BedTime,
    TimeOnly? FellAsleepTime,
    List<SleepIntervalDto> NightWakeUps,
    TimeOnly? MorningWakeTime,
    bool IsTired,
    List<SleepIntervalDto> Naps,
    int? TotalSleepMinutes,
    string Remarks,
    Guid LoggedBy);

internal sealed record SleepDiaryEntriesDto(string SleepHygieneNotes, List<SleepEntryDto> Entries);

internal sealed record ShareLinkDto(Guid Id, string Token, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt);

internal sealed record ShareLinkSummaryDto(Guid Id, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt);

internal sealed record SharedSleepDiaryDto(
    string ChildGivenName,
    string ChildFamilyName,
    DateOnly From,
    DateOnly To,
    DateTimeOffset? ExpiresAt,
    string SleepHygieneNotes,
    List<SleepEntryDto> Entries);

internal static class SleepDiaryTestHelpers
{
    public static readonly DateOnly Monday = new(2026, 3, 2);
    public static readonly DateOnly Saturday = new(2026, 3, 7);

    // A fully filled-in night, mirroring the example row of the clinical form.
    public static object FullNight(string remarks = "Cried for 20 minutes before settling") => new
    {
        RoutineStartTime = "19:00",
        RitualStartTime = "19:30",
        RitualEndTime = "20:10",
        BedTime = "20:15",
        FellAsleepTime = "20:45",
        NightWakeUps = new[] { new { StartTime = "03:30", DurationMinutes = 30 } },
        MorningWakeTime = "06:30",
        IsTired = true,
        Naps = new[] { new { StartTime = "13:00", DurationMinutes = 45 } },
        TotalSleepMinutes = 555,
        Remarks = remarks,
    };

    public static async Task<SleepEntryDto?> LogAsync(BuddyApiFixture fixture, string token, Guid childId, DateOnly date, object body, int expectedStatus = 200)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Json(body).ToUrl($"/sleep-diary/children/{childId}/entries/{date:yyyy-MM-dd}");
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<SleepEntryDto>() : null;
    }

    public static async Task ClearAsync(BuddyApiFixture fixture, string token, Guid childId, DateOnly date, int expectedStatus = 204) =>
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url($"/sleep-diary/children/{childId}/entries/{date:yyyy-MM-dd}");
            _.StatusCodeShouldBe(expectedStatus);
        });

    public static async Task UpdateNotesAsync(BuddyApiFixture fixture, string token, Guid childId, string? notes, int expectedStatus = 204) =>
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Json(new { Notes = notes }).ToUrl($"/sleep-diary/children/{childId}/hygiene-notes");
            _.StatusCodeShouldBe(expectedStatus);
        });

    public static async Task<SleepDiaryEntriesDto?> ListAsync(BuddyApiFixture fixture, string token, Guid childId, DateOnly from, DateOnly to, int expectedStatus = 200)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"/sleep-diary/children/{childId}/entries?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<SleepDiaryEntriesDto>() : null;
    }

    public static async Task<ShareLinkDto?> CreateShareLinkAsync(BuddyApiFixture fixture, string token, Guid childId, DateTimeOffset? expiresAt = null, int expectedStatus = 200)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(new { ExpiresAt = expiresAt }).ToUrl($"/sleep-diary/children/{childId}/share-links");
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<ShareLinkDto>() : null;
    }

    public static async Task<List<ShareLinkSummaryDto>?> ListShareLinksAsync(BuddyApiFixture fixture, string token, Guid childId, int expectedStatus = 200)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"/sleep-diary/children/{childId}/share-links");
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<List<ShareLinkSummaryDto>>() : null;
    }

    public static async Task RevokeShareLinkAsync(BuddyApiFixture fixture, string token, Guid childId, Guid shareLinkId, int expectedStatus = 204) =>
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url($"/sleep-diary/children/{childId}/share-links/{shareLinkId}");
            _.StatusCodeShouldBe(expectedStatus);
        });

    // Deliberately no Authorization header -- the share link is the only credential.
    public static async Task<SharedSleepDiaryDto?> GetSharedAsync(BuddyApiFixture fixture, string shareToken, string query = "", int expectedStatus = 200)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.Get.Url($"/sleep-diary/shared/{shareToken}{query}");
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<SharedSleepDiaryDto>() : null;
    }
}
