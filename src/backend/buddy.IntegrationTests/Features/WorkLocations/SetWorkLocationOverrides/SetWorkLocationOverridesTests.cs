using Alba;

using buddy.Features.Users;
using buddy.Features.WorkLocations;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.Features.WorkLocations.SetWorkLocationOverrides;

[Collection(BuddyApiCollection.Name)]
public sealed class SetWorkLocationOverridesTests(BuddyApiFixture fixture)
{
    private static readonly DateOnly Monday = new(2026, 9, 28);

    [Fact]
    [CoversEndpoint("SetWorkLocationOverrides")]
    public async Task An_override_beats_the_pattern_for_just_that_day()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var stil = await WorkLocationTestHelpers.AddLocationAsync(fixture, token, "Stil");
        var randers = await WorkLocationTestHelpers.AddLocationAsync(fixture, token, "Randers");
        await WorkLocationTestHelpers.ReplacePatternAsync(fixture, token, 1, Monday, new PatternDayDto(0, DayOfWeek.Friday, stil.Id));

        var friday = Monday.AddDays(4);
        var returned = await WorkLocationTestHelpers.SetOverridesAsync(fixture, token, friday, friday, randers.Id);

        var day = Assert.Single(returned);
        Assert.Equal(randers.Id, day.Location?.Id);
        Assert.Equal(WorkLocationTestHelpers.SourceOverride, day.Status.Source);

        var nextFriday = (await WorkLocationTestHelpers.ListDaysAsync(fixture, token, guardianId, friday.AddDays(7), friday.AddDays(7))).Single();
        Assert.Equal(stil.Id, nextFriday.Location?.Id);
        Assert.Equal(WorkLocationTestHelpers.SourcePattern, nextFriday.Status.Source);
    }

    [Fact]
    public async Task An_override_to_off_blanks_a_whole_holiday_range()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var stil = await WorkLocationTestHelpers.AddLocationAsync(fixture, token, "Stil");
        await WorkLocationTestHelpers.ReplacePatternAsync(fixture, token, 1, Monday,
            [.. Enum.GetValues<DayOfWeek>().Select(d => new PatternDayDto(0, d, stil.Id))]);

        await WorkLocationTestHelpers.SetOverridesAsync(fixture, token, Monday, Monday.AddDays(13), locationId: null);

        var days = await WorkLocationTestHelpers.ListDaysAsync(fixture, token, guardianId, Monday, Monday.AddDays(14));
        Assert.All(days.Take(14), d =>
        {
            Assert.Null(d.Location);
            Assert.Equal(WorkLocationTestHelpers.KindOff, d.Status.Kind);
        });
        Assert.Equal(stil.Id, days[14].Location?.Id);
    }

    [Fact]
    public async Task Only_dates_whose_override_changes_append_an_event()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var stil = await WorkLocationTestHelpers.AddLocationAsync(fixture, token, "Stil");
        await WorkLocationTestHelpers.SetOverridesAsync(fixture, token, Monday, Monday.AddDays(1), stil.Id);

        var store = fixture.Host.Services.GetRequiredService<IWorkLocationScheduleEventStore>();
        var id = WorkLocationScheduleId.ForGuardian(new UserId(guardianId));
        var before = (await store.ReadAsync(id, CancellationToken.None)).Count;

        await WorkLocationTestHelpers.SetOverridesAsync(fixture, token, Monday, Monday.AddDays(2), stil.Id);

        var newEvents = (await store.ReadAsync(id, CancellationToken.None)).Skip(before).ToList();
        var overridden = Assert.IsType<WorkLocationOverridden>(Assert.Single(newEvents).Value);
        Assert.Equal(Monday.AddDays(2), overridden.Date);
    }

    [Fact]
    public async Task Marking_the_same_days_off_again_appends_nothing()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        await WorkLocationTestHelpers.SetOverridesAsync(fixture, token, Monday, Monday.AddDays(1), locationId: null);

        var store = fixture.Host.Services.GetRequiredService<IWorkLocationScheduleEventStore>();
        var id = WorkLocationScheduleId.ForGuardian(new UserId(guardianId));
        var before = (await store.ReadAsync(id, CancellationToken.None)).Count;

        await WorkLocationTestHelpers.SetOverridesAsync(fixture, token, Monday, Monday.AddDays(1), locationId: null);

        Assert.Equal(before, (await store.ReadAsync(id, CancellationToken.None)).Count);
    }

    [Fact]
    public async Task A_range_longer_than_the_31_day_cap_or_backwards_is_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        foreach (var (from, to) in new[] { (Monday, Monday.AddDays(32)), (Monday, Monday.AddDays(-1)) })
        {
            await fixture.Host.Scenario(_ =>
            {
                _.WithRequestHeader("Authorization", $"Bearer {token}");
                _.Put.Json(new { From = from, To = to, LocationId = (Guid?)null }).ToUrl("/work-locations/me/overrides");
                _.StatusCodeShouldBe(400);
            });
        }
    }

    [Fact]
    public async Task An_unknown_location_is_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Json(new { From = Monday, To = Monday, LocationId = Guid.NewGuid() }).ToUrl("/work-locations/me/overrides");
            _.StatusCodeShouldBe(400);
        });
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
            _.Put.Json(new { From = Monday, To = Monday, LocationId = (Guid?)null }).ToUrl("/work-locations/me/overrides");
            _.StatusCodeShouldBe(403);
        });
    }
}
