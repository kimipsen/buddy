using Alba;

using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.WorkLocations.ListWorkDays;

[Collection(BuddyApiCollection.Name)]
public sealed class ListWorkDaysTests(BuddyApiFixture fixture)
{
    private static readonly DateOnly Monday = new(2026, 9, 28);

    [Fact]
    [CoversEndpoint("ListWorkDays")]
    public async Task A_co_guardian_sees_the_resolved_days_for_a_week()
    {
        var family = await WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);
        var randers = await WorkLocationTestHelpers.AddLocationAsync(fixture, family.FirstToken, "Randers", "🚆", "#dc2626");
        await WorkLocationTestHelpers.ReplacePatternAsync(fixture, family.FirstToken, 1, Monday,
            new PatternDayDto(0, DayOfWeek.Monday, randers.Id),
            new PatternDayDto(0, DayOfWeek.Wednesday, randers.Id));

        var days = await WorkLocationTestHelpers.ListDaysAsync(fixture, family.SecondToken, family.FirstId, Monday, Monday.AddDays(6));

        Assert.Equal(7, days.Count);
        Assert.Equal(Enumerable.Range(0, 7).Select(i => Monday.AddDays(i)), days.Select(d => d.Date));
        Assert.Equal(new WorkLocationDto(randers.Id, "Randers", "🚆", "#dc2626", false), days[0].Location);
        Assert.Null(days[1].Location);
        Assert.Equal(WorkLocationTestHelpers.SourceNone, days[1].Source);
        Assert.Equal(randers.Id, days[2].Location?.Id);
    }

    [Fact]
    public async Task Cycle_weeks_count_from_the_anchor_across_iso_week_53_and_before_the_anchor()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var stil = await WorkLocationTestHelpers.AddLocationAsync(fixture, token, "Stil");
        var anchor = new DateOnly(2026, 12, 21); // ISO week 52; 2026-12-28 starts ISO week 53
        await WorkLocationTestHelpers.ReplacePatternAsync(fixture, token, 2, anchor, new PatternDayDto(0, DayOfWeek.Monday, stil.Id));

        var stilMondays = (await WorkLocationTestHelpers.ListDaysAsync(fixture, token, guardianId, new DateOnly(2026, 12, 7), new DateOnly(2027, 1, 4)))
            .Where(d => d.Location is not null)
            .Select(d => d.Date)
            .ToList();

        // 2026-12-07 is two weeks before the anchor (cycle week 0); 2027-01-04 (ISO week 1) is two
        // weeks after it. ISO parity would have put both 2026-12-28 (week 53) and 2027-01-04 (week 1)
        // in the same "odd" bucket.
        Assert.Equal([new DateOnly(2026, 12, 7), new DateOnly(2026, 12, 21), new DateOnly(2027, 1, 4)], stilMondays);
    }

    [Fact]
    public async Task A_co_guardian_who_leaves_the_family_loses_access_immediately()
    {
        var family = await WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);
        await WorkLocationTestHelpers.ListDaysAsync(fixture, family.SecondToken, family.FirstId, Monday, Monday);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {family.SecondToken}");
            _.Delete.Url($"/users/me/children/{family.Child.Id}/guardian-link");
            _.StatusCodeShouldBe(204);
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {family.SecondToken}");
            _.Get.Url($"/work-locations/guardians/{family.FirstId}/days?from={Monday:yyyy-MM-dd}&to={Monday:yyyy-MM-dd}");
            _.StatusCodeShouldBe(404);
        });
    }

    [Fact]
    public async Task The_guardians_own_child_gets_not_found()
    {
        var (_, guardianToken, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Get.Url($"/work-locations/guardians/{guardianId}/days?from={Monday:yyyy-MM-dd}&to={Monday:yyyy-MM-dd}");
            _.StatusCodeShouldBe(404);
        });
    }

    [Fact]
    public async Task An_unrelated_user_gets_not_found()
    {
        var (_, _, ownerId) = await fixture.CreateAuthenticatedUserAsync();
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {strangerToken}");
            _.Get.Url($"/work-locations/guardians/{ownerId}/days?from={Monday:yyyy-MM-dd}&to={Monday:yyyy-MM-dd}");
            _.StatusCodeShouldBe(404);
        });
    }

    [Fact]
    public async Task A_range_longer_than_the_31_day_cap_is_rejected()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"/work-locations/guardians/{guardianId}/days?from={Monday:yyyy-MM-dd}&to={Monday.AddDays(32):yyyy-MM-dd}");
            _.StatusCodeShouldBe(400);
        });
    }
}
