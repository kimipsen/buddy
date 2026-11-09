using Alba;

using buddy.Features.NightDuty;
using buddy.IntegrationTests.Fixtures;

namespace buddy.IntegrationTests.Features.NightDuty;

// Response shapes, matching Night / NightStatus / NightEntryDto / NightScheduleResponse
// (Features/NightDuty/Types). Enums travel as their member name; the "kind" discriminators stay numeric.
internal sealed record ResponsibleTestDto(
    int Kind, Guid? GuardianId = null, Guid? BabysitterId = null, string? Name = null, string? HostName = null, string? Location = null, string? ContactInfo = null);

internal sealed record NightEntryTestDto(ResponsibleTestDto Responsible, string Note);

internal sealed record NightStatusTestDto(int Kind, NightEntryTestDto? Entry = null, NightSource? Source = null, NightEntryTestDto? Scheduled = null);

internal sealed record NightTestDto(DateOnly Date, NightStatusTestDto Status);

internal sealed record NightPatternDayTestDto(int Week, DayOfWeek Day, NightEntryTestDto Entry);

internal sealed record NightPatternTestDto(DateOnly EffectiveFrom, int CycleWeeks, DateOnly AnchorMonday, List<NightPatternDayTestDto> Days, NightPatternStatus Status);

internal sealed record NightScheduleTestDto(Guid ChildId, DateOnly Today, List<NightPatternTestDto> Patterns);

internal static class NightDutyTestHelpers
{
    public const int KindUnplanned = 0;
    public const int KindCleared = 1;
    public const int KindPlanned = 2;

    public const int ResponsibleGuardian = 0;
    public const int ResponsibleBabysitter = 1;
    public const int ResponsibleSleepover = 2;
    public const int ResponsibleUnassigned = 3;

    // Far enough ahead that no time zone puts it in the child's past, and a Monday so cycle weeks
    // line up with calendar weeks.
    public static DateOnly FutureMonday(int weeksAhead = 2)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));

        return monday.AddDays(7 * weeksAhead);
    }

    public static object Guardian(Guid guardianId, string? note = null) => new { Responsible = new { Kind = ResponsibleGuardian, GuardianId = guardianId }, Note = note };

    public static object Babysitter(Guid guardianId, Guid babysitterId, string? note = null) =>
        new { Responsible = new { Kind = ResponsibleBabysitter, GuardianId = guardianId, BabysitterId = babysitterId }, Note = note };

    public static object Sleepover(string hostName, string? location = null, string? note = null) =>
        new { Responsible = new { Kind = ResponsibleSleepover, HostName = hostName, Location = location }, Note = note };

    public static object NoteOnly(string note) => new { Responsible = new { Kind = ResponsibleUnassigned }, Note = note };

    public static object Day(int week, DayOfWeek day, object entry) => new { Week = week, Day = day, Entry = entry };

    public static object Override(DateOnly date, object? entry) => new { Date = date, Entry = entry };

    public static async Task<NightScheduleTestDto> ReplacePatternAsync(
        BuddyApiFixture fixture, string token, Guid childId, DateOnly? effectiveFrom, int cycleWeeks, DateOnly anchorMonday, params object[] days)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Json(new { EffectiveFrom = effectiveFrom, CycleWeeks = cycleWeeks, AnchorMonday = anchorMonday, Days = days })
                .ToUrl($"/night-duty/children/{childId}/pattern");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<NightScheduleTestDto>();
    }

    public static async Task<IScenarioResult> ReplacePatternExpectingAsync(
        BuddyApiFixture fixture, string token, Guid childId, int status, object body) =>
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Json(body).ToUrl($"/night-duty/children/{childId}/pattern");
            _.StatusCodeShouldBe(status);
        });

    public static async Task<List<NightTestDto>> SetOverridesAsync(BuddyApiFixture fixture, string token, Guid childId, params object[] nights)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Json(new { Nights = nights }).ToUrl($"/night-duty/children/{childId}/overrides");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<List<NightTestDto>>();
    }

    public static async Task<IScenarioResult> SetOverridesExpectingAsync(BuddyApiFixture fixture, string token, Guid childId, int status, params object[] nights) =>
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Json(new { Nights = nights }).ToUrl($"/night-duty/children/{childId}/overrides");
            _.StatusCodeShouldBe(status);
        });

    public static async Task ClearOverridesAsync(BuddyApiFixture fixture, string token, Guid childId, DateOnly from, DateOnly to, int status = 204) =>
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url($"/night-duty/children/{childId}/overrides?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");
            _.StatusCodeShouldBe(status);
        });

    public static async Task<List<NightTestDto>> ListNightsAsync(BuddyApiFixture fixture, string token, Guid childId, DateOnly from, DateOnly to)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"/night-duty/children/{childId}/nights?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<List<NightTestDto>>();
    }

    public static async Task ListNightsExpectingAsync(BuddyApiFixture fixture, string token, Guid childId, DateOnly from, DateOnly to, int status) =>
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"/night-duty/children/{childId}/nights?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");
            _.StatusCodeShouldBe(status);
        });

    public static async Task<NightScheduleTestDto> GetScheduleAsync(BuddyApiFixture fixture, string token, Guid childId)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"/night-duty/children/{childId}");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<NightScheduleTestDto>();
    }

    public static async Task GetScheduleExpectingAsync(BuddyApiFixture fixture, string token, Guid childId, int status) =>
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"/night-duty/children/{childId}");
            _.StatusCodeShouldBe(status);
        });
}
