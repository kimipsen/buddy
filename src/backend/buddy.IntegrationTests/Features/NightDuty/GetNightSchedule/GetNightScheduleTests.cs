using buddy.Features.NightDuty;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

using static buddy.IntegrationTests.Features.NightDuty.NightDutyTestHelpers;

namespace buddy.IntegrationTests.Features.NightDuty.GetNightSchedule;

[Collection(BuddyApiCollection.Name)]
public sealed class GetNightScheduleTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("GetNightSchedule")]
    public async Task A_guardian_sees_every_version_flagged_current_or_upcoming()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var monday = FutureMonday();
        await ReplacePatternAsync(fixture, token, child.Id, effectiveFrom: null, 1, monday, Day(0, DayOfWeek.Monday, Guardian(guardianId)));
        await ReplacePatternAsync(fixture, token, child.Id, monday, 2, monday, Day(1, DayOfWeek.Friday, NoteOnly("Screen night")));

        var schedule = await GetScheduleAsync(fixture, token, child.Id);

        Assert.Equal(child.Id, schedule.ChildId);
        Assert.Equal([NightPatternStatus.Current, NightPatternStatus.Upcoming], schedule.Patterns.Select(p => p.Status));
        Assert.Equal(2, schedule.Patterns[1].CycleWeeks);
        Assert.Equal("Screen night", Assert.Single(schedule.Patterns[1].Days).Entry.Note);
    }

    [Fact]
    public async Task A_child_with_no_schedule_has_no_versions()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);

        Assert.Empty((await GetScheduleAsync(fixture, token, child.Id)).Patterns);
    }

    [Fact]
    public async Task The_child_gets_forbidden_and_a_stranger_gets_not_found()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await GetScheduleExpectingAsync(fixture, childToken, child.Id, 403);
        await GetScheduleExpectingAsync(fixture, strangerToken, child.Id, 404);
    }
}
