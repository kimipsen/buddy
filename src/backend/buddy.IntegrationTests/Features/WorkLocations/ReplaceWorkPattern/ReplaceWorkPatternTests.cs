using Alba;

using buddy.Common;
using buddy.Features.Users;
using buddy.Features.WorkLocations;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.Features.WorkLocations.ReplaceWorkPattern;

[Collection(BuddyApiCollection.Name)]
public sealed class ReplaceWorkPatternTests(BuddyApiFixture fixture)
{
    private static readonly DateOnly Monday = new(2026, 9, 28);

    [Fact]
    [CoversEndpoint("ReplaceWorkPattern")]
    public async Task An_alternating_two_week_pattern_resolves_per_cycle_week()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var stil = await WorkLocationTestHelpers.AddLocationAsync(fixture, token, "Stil");

        // Week A: Tuesday + Thursday at Stil. Week B: Thursday only. Sent out of order on purpose.
        var pattern = await WorkLocationTestHelpers.ReplacePatternAsync(fixture, token, 2, Monday,
            new PatternDayDto(1, DayOfWeek.Thursday, stil.Id),
            new PatternDayDto(0, DayOfWeek.Thursday, stil.Id),
            new PatternDayDto(0, DayOfWeek.Tuesday, stil.Id));

        Assert.Equal(2, pattern.CycleWeeks);
        Assert.Equal(
            [(0, DayOfWeek.Tuesday), (0, DayOfWeek.Thursday), (1, DayOfWeek.Thursday)],
            pattern.Days.Select(d => (d.Week, d.Day)));

        var days = await WorkLocationTestHelpers.ListDaysAsync(fixture, token, guardianId, Monday, Monday.AddDays(13));
        var stilDays = days.Where(d => d.Location?.Id == stil.Id).Select(d => d.Date).ToList();

        Assert.Equal([Monday.AddDays(1), Monday.AddDays(3), Monday.AddDays(10)], stilDays);
        Assert.All(days.Where(d => d.Location is not null), d => Assert.Equal(WorkLocationTestHelpers.SourcePattern, d.Source));
    }

    [Fact]
    public async Task Saving_the_same_pattern_appends_no_event()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var stil = await WorkLocationTestHelpers.AddLocationAsync(fixture, token, "Stil");
        await WorkLocationTestHelpers.ReplacePatternAsync(fixture, token, 1, Monday, new PatternDayDto(0, DayOfWeek.Monday, stil.Id));

        var store = fixture.Host.Services.GetRequiredService<IWorkLocationScheduleEventStore>();
        var id = WorkLocationScheduleId.ForGuardian(new UserId(guardianId));
        var before = (await store.ReadAsync(id, CancellationToken.None)).Count;

        await WorkLocationTestHelpers.ReplacePatternAsync(fixture, token, 1, Monday, new PatternDayDto(0, DayOfWeek.Monday, stil.Id));

        Assert.Equal(before, (await store.ReadAsync(id, CancellationToken.None)).Count);
    }

    public static TheoryData<int, string, int, int, string> InvalidPatterns => new()
    {
        { 0, "2026-09-28", 0, 1, "cycleWeeks" },   // cycle too short
        { 5, "2026-09-28", 0, 1, "cycleWeeks" },   // cycle too long
        { 2, "2026-09-29", 0, 1, "anchorMonday" }, // anchor is a Tuesday
        { 2, "2026-09-28", 2, 1, "days" },         // week outside the cycle
        { 2, "2026-09-28", -1, 1, "days" },        // negative week
        { 2, "2026-09-28", 0, 9, "days" },         // not a weekday
    };

    [Theory]
    [MemberData(nameof(InvalidPatterns))]
    public async Task A_structurally_invalid_pattern_is_rejected_on_the_offending_field(int cycleWeeks, string anchor, int week, int day, string field)
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var stil = await WorkLocationTestHelpers.AddLocationAsync(fixture, token, "Stil");

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Json(new { CycleWeeks = cycleWeeks, AnchorMonday = anchor, Days = new[] { new { Week = week, Day = day, LocationId = stil.Id } } })
                .ToUrl("/work-locations/me/pattern");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Contains(error.Details.Keys, key => key.StartsWith(field, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_null_day_entry_is_rejected_rather_than_crashing()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Json(new { CycleWeeks = 1, AnchorMonday = Monday, Days = new object?[] { null } }).ToUrl("/work-locations/me/pattern");
            _.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task Two_locations_on_the_same_week_and_weekday_are_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var stil = await WorkLocationTestHelpers.AddLocationAsync(fixture, token, "Stil");
        var randers = await WorkLocationTestHelpers.AddLocationAsync(fixture, token, "Randers");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Json(new
            {
                CycleWeeks = 1,
                AnchorMonday = Monday,
                Days = new[] { new PatternDayDto(0, DayOfWeek.Monday, stil.Id), new PatternDayDto(0, DayOfWeek.Monday, randers.Id) }
            }).ToUrl("/work-locations/me/pattern");
            _.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task An_unknown_or_archived_location_is_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var stil = await WorkLocationTestHelpers.AddLocationAsync(fixture, token, "Stil");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url($"/work-locations/me/locations/{stil.Id}");
            _.StatusCodeShouldBe(204);
        });

        foreach (var locationId in new[] { stil.Id, Guid.NewGuid() })
        {
            await fixture.Host.Scenario(_ =>
            {
                _.WithRequestHeader("Authorization", $"Bearer {token}");
                _.Put.Json(new { CycleWeeks = 1, AnchorMonday = Monday, Days = new[] { new PatternDayDto(0, DayOfWeek.Monday, locationId) } })
                    .ToUrl("/work-locations/me/pattern");
                _.StatusCodeShouldBe(400);
            });
        }
    }

    [Fact]
    public async Task A_child_account_is_forbidden()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Put.Json(new { CycleWeeks = 1, AnchorMonday = Monday, Days = Array.Empty<object>() }).ToUrl("/work-locations/me/pattern");
            _.StatusCodeShouldBe(403);
        });
    }
}
