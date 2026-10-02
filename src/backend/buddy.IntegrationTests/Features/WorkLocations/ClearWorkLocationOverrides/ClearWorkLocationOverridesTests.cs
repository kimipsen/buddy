using Alba;

using buddy.Features.Users;
using buddy.Features.WorkLocations;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.Features.WorkLocations.ClearWorkLocationOverrides;

[Collection(BuddyApiCollection.Name)]
public sealed class ClearWorkLocationOverridesTests(BuddyApiFixture fixture)
{
    private static readonly DateOnly Monday = new(2026, 9, 28);

    [Fact]
    [CoversEndpoint("ClearWorkLocationOverrides")]
    public async Task Clearing_reverts_the_days_to_the_pattern()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var stil = await WorkLocationTestHelpers.AddLocationAsync(fixture, token, "Stil");
        await WorkLocationTestHelpers.ReplacePatternAsync(fixture, token, 1, Monday, new PatternDayDto(0, DayOfWeek.Tuesday, stil.Id));
        await WorkLocationTestHelpers.SetOverridesAsync(fixture, token, Monday, Monday.AddDays(6), locationId: null);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url($"/work-locations/me/overrides?from={Monday:yyyy-MM-dd}&to={Monday.AddDays(2):yyyy-MM-dd}");
            _.StatusCodeShouldBe(204);
        });

        var days = await WorkLocationTestHelpers.ListDaysAsync(fixture, token, guardianId, Monday, Monday.AddDays(3));
        Assert.Equal(WorkLocationTestHelpers.KindUnplanned, days[0].Status.Kind);
        Assert.Equal(WorkLocationTestHelpers.SourcePattern, days[1].Status.Source);
        Assert.Equal(stil.Id, days[1].Location?.Id);
        Assert.Equal(WorkLocationTestHelpers.KindUnplanned, days[2].Status.Kind);
        Assert.Equal(WorkLocationTestHelpers.KindOff, days[3].Status.Kind);
    }

    [Fact]
    public async Task Clearing_when_nothing_is_overridden_is_a_no_op_that_starts_no_stream()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url($"/work-locations/me/overrides?from={Monday:yyyy-MM-dd}&to={Monday:yyyy-MM-dd}");
            _.StatusCodeShouldBe(204);
        });

        var store = fixture.Host.Services.GetRequiredService<IWorkLocationScheduleEventStore>();
        Assert.Empty(await store.ReadAsync(WorkLocationScheduleId.ForGuardian(new UserId(guardianId)), CancellationToken.None));
    }

    [Fact]
    public async Task A_range_longer_than_the_31_day_cap_is_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url($"/work-locations/me/overrides?from={Monday:yyyy-MM-dd}&to={Monday.AddDays(32):yyyy-MM-dd}");
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
            _.Delete.Url($"/work-locations/me/overrides?from={Monday:yyyy-MM-dd}&to={Monday:yyyy-MM-dd}");
            _.StatusCodeShouldBe(403);
        });
    }
}
