using Alba;

using buddy.Features.Guardians;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;

namespace buddy.IntegrationTests.Features.WorkLocations;

// Response shapes, matching WorkLocationSummary / WorkLocationScheduleResponse / WorkDay
// (Features/WorkLocations/Types). Enums travel as their ordinal over HTTP, like everywhere else.
internal sealed record WorkLocationDto(Guid Id, string Name, string Icon, string Color, bool IsArchived);

internal sealed record PatternDayDto(int Week, DayOfWeek Day, Guid LocationId);

internal sealed record WorkPatternDto(int CycleWeeks, DateOnly AnchorMonday, List<PatternDayDto> Days);

internal sealed record WorkLocationScheduleDto(Guid GuardianId, List<WorkLocationDto> Locations, WorkPatternDto Pattern);

internal sealed record WorkDayDto(DateOnly Date, WorkLocationDto? Location, int Source);

internal static class WorkLocationTestHelpers
{
    public const int SourceNone = 0;
    public const int SourcePattern = 1;
    public const int SourceOverride = 2;

    public static async Task<WorkLocationDto> AddLocationAsync(BuddyApiFixture fixture, string token, string name, string icon = "🏢", string color = "#2563eb")
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(new { Name = name, Icon = icon, Color = color }).ToUrl("/work-locations/me/locations");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<WorkLocationDto>();
    }

    public static async Task<WorkPatternDto> ReplacePatternAsync(BuddyApiFixture fixture, string token, int cycleWeeks, DateOnly anchorMonday, params PatternDayDto[] days)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Json(new { CycleWeeks = cycleWeeks, AnchorMonday = anchorMonday, Days = days }).ToUrl("/work-locations/me/pattern");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<WorkPatternDto>();
    }

    public static async Task<List<WorkDayDto>> SetOverridesAsync(BuddyApiFixture fixture, string token, DateOnly from, DateOnly to, Guid? locationId)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Json(new { From = from, To = to, LocationId = locationId }).ToUrl("/work-locations/me/overrides");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<List<WorkDayDto>>();
    }

    public static async Task<List<WorkDayDto>> ListDaysAsync(BuddyApiFixture fixture, string token, Guid guardianId, DateOnly from, DateOnly to)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"/work-locations/guardians/{guardianId}/days?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<List<WorkDayDto>>();
    }

    // Two guardians of the same child: the first creates the child, the second accepts an invite.
    public static async Task<CoGuardians> CreateCoGuardiansAsync(BuddyApiFixture fixture)
    {
        var (_, firstToken, firstId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, firstToken, "Alex");
        var (second, secondToken, secondId) = await fixture.CreateAuthenticatedUserAsync();

        await GuardianTestHelpers.InviteGuardianAsync(fixture, firstToken, child.Id, second.Email, GuardianKind.Parent);
        var inviteToken = await GuardianTestHelpers.ReadGuardianInviteTokenAsync(fixture, second.Email);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {secondToken}");
            _.Post.Url($"/guardian-invites/{inviteToken}/accept");
            _.StatusCodeShouldBe(204);
        });

        return new CoGuardians(firstToken, firstId, secondToken, secondId, child);
    }
}

internal sealed record CoGuardians(string FirstToken, Guid FirstId, string SecondToken, Guid SecondId, ChildResponseDto Child);
