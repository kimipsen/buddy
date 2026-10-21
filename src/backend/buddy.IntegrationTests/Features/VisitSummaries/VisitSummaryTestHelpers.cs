using Alba;

using buddy.IntegrationTests.Fixtures;

namespace buddy.IntegrationTests.Features.VisitSummaries;

// Response shapes matching VisitSummaryResponse / VisitSummaryWithReportResponse /
// SharedVisitSummaryResponse / VisitSummaryShareLinkResponse (Features/VisitSummaries). Enums
// travel by name. The report's sections are kept as their few fields the tests look at.
internal sealed record VisitSummaryLinkSummaryDto(Guid Id, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt);

internal sealed record VisitSummaryDto(
    Guid Id,
    Guid ChildId,
    string Title,
    DateOnly? VisitDate,
    DateOnly From,
    DateOnly To,
    List<string> Sections,
    string Note,
    Guid CreatedBy,
    DateTimeOffset LastModifiedAt,
    List<VisitSummaryLinkSummaryDto> ShareLinks);

internal sealed record ReportSleepEntryDto(DateOnly Date, int? TotalSleepMinutes);

internal sealed record ReportSleepDto(string SleepHygieneNotes, List<ReportSleepEntryDto> Entries);

internal sealed record ReportScheduleDto(Guid Id, string Name, string Dosage, bool IsStopped);

internal sealed record ReportDoseDto(Guid MedicineId, string Name, DateOnly Date, TimeOnly Time, string Status);

internal sealed record ReportRegimenDto(Guid MedicineId, string Name);

internal sealed record ReportEffectNoteDto(DateOnly Date, string Note, List<ReportRegimenDto> Regimen);

internal sealed record ReportMedicineDto(
    List<ReportScheduleDto> Schedules, List<ReportDoseDto> Doses, List<ReportEffectNoteDto> EffectNotes, int ExcludedMedicineCount);

internal sealed record ReportCheckInDto(string Zone, string Note, bool IsSelfReport);

internal sealed record ReportFeelingsDto(List<ReportCheckInDto> CheckIns);

internal sealed record VisitSummaryReportDto(
    string ChildGivenName,
    string ChildFamilyName,
    string Title,
    DateOnly? VisitDate,
    DateOnly From,
    DateOnly To,
    string Note,
    string PreparedBy,
    ReportSleepDto? Sleep,
    ReportMedicineDto? Medicine,
    ReportFeelingsDto? Feelings);

internal sealed record VisitSummaryWithReportDto(VisitSummaryDto Summary, VisitSummaryReportDto Report);

internal sealed record SharedVisitSummaryDto(VisitSummaryReportDto Report, DateTimeOffset? ExpiresAt);

internal sealed record VisitSummaryShareLinkDto(Guid Id, string Token, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt);

internal static class VisitSummaryTestHelpers
{
    public static readonly DateOnly From = new(2026, 3, 2);
    public static readonly DateOnly To = new(2026, 3, 29);

    public static object Body(
        string title = "Follow-up with Dr. Lund",
        DateOnly? from = null,
        DateOnly? to = null,
        string[]? sections = null,
        string note = "Afternoons are harder since the dose change.",
        DateOnly? visitDate = null) => new
        {
            Title = title,
            VisitDate = visitDate ?? new DateOnly(2026, 4, 2),
            From = from ?? From,
            To = to ?? To,
            Sections = sections ?? ["Medicine", "Sleep", "Feelings"],
            Note = note
        };

    public static async Task<VisitSummaryDto> CreateAsync(BuddyApiFixture fixture, string token, Guid childId, object? body = null) =>
        (await SendAsync(fixture, token, s => s.Post.Json(body ?? Body()).ToUrl($"/visit-summaries/children/{childId}"), 200))
            .ReadAsJson<VisitSummaryDto>()!;

    public static Task<IScenarioResult> CreateRawAsync(BuddyApiFixture fixture, string token, Guid childId, object body, int expectedStatus) =>
        SendAsync(fixture, token, s => s.Post.Json(body).ToUrl($"/visit-summaries/children/{childId}"), expectedStatus);

    public static Task<IScenarioResult> UpdateAsync(BuddyApiFixture fixture, string token, Guid childId, Guid summaryId, object body, int expectedStatus = 200) =>
        SendAsync(fixture, token, s => s.Put.Json(body).ToUrl($"/visit-summaries/children/{childId}/{summaryId}"), expectedStatus);

    public static Task<IScenarioResult> DeleteAsync(BuddyApiFixture fixture, string token, Guid childId, Guid summaryId, int expectedStatus = 204) =>
        SendAsync(fixture, token, s => s.Delete.Url($"/visit-summaries/children/{childId}/{summaryId}"), expectedStatus);

    public static async Task<List<VisitSummaryDto>?> ListAsync(BuddyApiFixture fixture, string token, Guid childId, int expectedStatus = 200)
    {
        var response = await SendAsync(fixture, token, s => s.Get.Url($"/visit-summaries/children/{childId}"), expectedStatus);

        return expectedStatus == 200 ? response.ReadAsJson<List<VisitSummaryDto>>() : null;
    }

    public static async Task<VisitSummaryWithReportDto?> GetAsync(BuddyApiFixture fixture, string token, Guid childId, Guid summaryId, int expectedStatus = 200) =>
        await GetAsync(fixture.Host, token, childId, summaryId, expectedStatus);

    public static async Task<VisitSummaryWithReportDto?> GetAsync(IAlbaHost host, string token, Guid childId, Guid summaryId, int expectedStatus = 200)
    {
        var response = await SendAsync(host, token, s => s.Get.Url($"/visit-summaries/children/{childId}/{summaryId}"), expectedStatus);

        return expectedStatus == 200 ? response.ReadAsJson<VisitSummaryWithReportDto>() : null;
    }

    public static async Task<VisitSummaryShareLinkDto> CreateShareLinkAsync(
        BuddyApiFixture fixture, string token, Guid childId, Guid summaryId, DateTimeOffset? expiresAt = null) =>
        (await CreateShareLinkRawAsync(fixture, token, childId, summaryId, expiresAt, 200)).ReadAsJson<VisitSummaryShareLinkDto>()!;

    public static Task<IScenarioResult> CreateShareLinkRawAsync(
        BuddyApiFixture fixture, string token, Guid childId, Guid summaryId, DateTimeOffset? expiresAt, int expectedStatus) =>
        SendAsync(fixture, token, s => s.Post.Json(new { ExpiresAt = expiresAt }).ToUrl($"/visit-summaries/children/{childId}/{summaryId}/share-links"), expectedStatus);

    public static Task<IScenarioResult> RevokeShareLinkAsync(BuddyApiFixture fixture, string token, Guid childId, Guid summaryId, Guid linkId, int expectedStatus = 204) =>
        SendAsync(fixture, token, s => s.Delete.Url($"/visit-summaries/children/{childId}/{summaryId}/share-links/{linkId}"), expectedStatus);

    public static async Task<SharedVisitSummaryDto?> GetSharedAsync(BuddyApiFixture fixture, string shareToken, int expectedStatus = 200) =>
        await GetSharedAsync(fixture.Host, shareToken, expectedStatus);

    public static async Task<SharedVisitSummaryDto?> GetSharedAsync(IAlbaHost host, string shareToken, int expectedStatus = 200)
    {
        var response = await host.Scenario(_ =>
        {
            _.Get.Url($"/visit-summaries/shared/{shareToken}");
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<SharedVisitSummaryDto>() : null;
    }

    private static Task<IScenarioResult> SendAsync(BuddyApiFixture fixture, string token, Action<Scenario> request, int expectedStatus) =>
        SendAsync(fixture.Host, token, request, expectedStatus);

    private static Task<IScenarioResult> SendAsync(IAlbaHost host, string token, Action<Scenario> request, int expectedStatus) =>
        host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            request(_);
            _.StatusCodeShouldBe(expectedStatus);
        });
}
