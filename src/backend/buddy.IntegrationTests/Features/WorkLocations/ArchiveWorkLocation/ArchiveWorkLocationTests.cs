using Alba;

using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.WorkLocations.ArchiveWorkLocation;

[Collection(BuddyApiCollection.Name)]
public sealed class ArchiveWorkLocationTests(BuddyApiFixture fixture)
{
    private static readonly DateOnly Monday = new(2026, 9, 28);

    [Fact]
    [CoversEndpoint("ArchiveWorkLocation")]
    public async Task Archiving_removes_the_location_from_the_pattern_but_overrides_keep_resolving()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var stil = await WorkLocationTestHelpers.AddLocationAsync(fixture, token, "Stil");
        var randers = await WorkLocationTestHelpers.AddLocationAsync(fixture, token, "Randers");
        await WorkLocationTestHelpers.ReplacePatternAsync(fixture, token, 1, Monday,
            new PatternDayDto(0, DayOfWeek.Tuesday, stil.Id),
            new PatternDayDto(0, DayOfWeek.Friday, randers.Id));
        await WorkLocationTestHelpers.SetOverridesAsync(fixture, token, Monday.AddDays(2), Monday.AddDays(2), stil.Id);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url($"/work-locations/me/locations/{stil.Id}");
            _.StatusCodeShouldBe(204);
        });

        var schedule = (await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"/work-locations/guardians/{guardianId}");
            _.StatusCodeShouldBeOk();
        })).ReadAsJson<WorkLocationScheduleDto>();

        Assert.True(schedule.Locations.Single(l => l.Id == stil.Id).IsArchived);
        var remaining = Assert.Single(schedule.Pattern.Days);
        Assert.Equal(randers.Id, remaining.LocationId);

        var days = await WorkLocationTestHelpers.ListDaysAsync(fixture, token, guardianId, Monday, Monday.AddDays(6));
        Assert.Equal(WorkLocationTestHelpers.SourceNone, days[1].Source);       // Tuesday: was Stil in the pattern
        Assert.Equal(stil.Id, days[2].Location?.Id);                            // Wednesday: override still resolves
        Assert.True(days[2].Location?.IsArchived);
        Assert.Equal(randers.Id, days[4].Location?.Id);                         // Friday: untouched
    }

    [Fact]
    public async Task Archiving_twice_is_idempotent()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var location = await WorkLocationTestHelpers.AddLocationAsync(fixture, token, "Stil");

        for (var i = 0; i < 2; i++)
        {
            await fixture.Host.Scenario(_ =>
            {
                _.WithRequestHeader("Authorization", $"Bearer {token}");
                _.Delete.Url($"/work-locations/me/locations/{location.Id}");
                _.StatusCodeShouldBe(204);
            });
        }
    }

    [Fact]
    public async Task An_unknown_location_is_not_found()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url($"/work-locations/me/locations/{Guid.NewGuid()}");
            _.StatusCodeShouldBe(404);
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
            _.Delete.Url($"/work-locations/me/locations/{Guid.NewGuid()}");
            _.StatusCodeShouldBe(403);
        });
    }
}
