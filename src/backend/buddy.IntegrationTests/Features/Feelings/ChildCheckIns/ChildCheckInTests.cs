using buddy.Features.Feelings;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.Feelings.FeelingsTestHelpers;

namespace buddy.IntegrationTests.Features.Feelings.ChildCheckIns;

// The child's own /me routes: CheckIn, GetMyFeelingsToday and UndoCheckIn (the undo window itself
// is in UndoWindowTests, on a fake clock).
[Collection(BuddyApiCollection.Name)]
public sealed class ChildCheckInTests(BuddyApiFixture fixture)
{
    private const string Copenhagen = "Europe/Copenhagen";

    [Fact]
    [CoversEndpoint("CheckIn")]
    [CoversEndpoint("GetMyFeelingsToday")]
    public async Task A_child_checks_in_and_sees_it_in_today()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Emil");
        await SetChildTimeZoneAsync(fixture, guardianToken, child.Id, Copenhagen);
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var (today, _) = LocalNow(Copenhagen);

        var checkIn = await CheckInAsync(fixture, childToken, new { Zone = "Yellow", Tags = new[] { "Worried", "School" }, Note = "  Maths test  " });

        Assert.NotNull(checkIn);
        Assert.Equal("Yellow", checkIn.Zone);
        Assert.Equal(["Worried", "School"], checkIn.Tags);
        Assert.Equal("Maths test", checkIn.Note);
        Assert.Equal(today, checkIn.Date);
        Assert.Equal(child.Id, checkIn.LoggedBy);
        Assert.True(checkIn.IsSelfReport);

        var mine = await GetTodayAsync(fixture, childToken);

        Assert.Equal(today, mine!.Date);
        Assert.Equal("FacesAndWords", mine.DisplayMode);
        Assert.Empty(mine.PromptTimes);
        Assert.Equal(checkIn.Id, Assert.Single(mine.CheckIns).Id);
    }

    [Fact]
    public async Task A_zone_alone_is_enough()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Emil");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        var checkIn = await CheckInAsync(fixture, childToken, new { Zone = "Green" });

        Assert.Empty(checkIn!.Tags);
        Assert.Equal("", checkIn.Note);
    }

    [Fact]
    public async Task The_child_sees_only_their_own_check_ins_for_today_never_a_guardians_observation()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Emil");
        await SetChildTimeZoneAsync(fixture, guardianToken, child.Id, Copenhagen);
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var (today, _) = LocalNow(Copenhagen);

        var own = await CheckInAsync(fixture, childToken, new { Zone = "Green" });
        await LogAsync(fixture, guardianToken, child.Id, new { Zone = "Red", Note = "Meltdown at pickup" });
        await LogAsync(fixture, guardianToken, child.Id, new { Zone = "Blue", Date = today.AddDays(-1), Time = "15:00" });

        var mine = await GetTodayAsync(fixture, childToken);

        Assert.Equal(own!.Id, Assert.Single(mine!.CheckIns).Id);
    }

    [Fact]
    public async Task The_childs_date_comes_from_their_own_time_zone()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Emil");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        // Two zones 23 hours apart: at least one of them is on another date than UTC at any moment.
        foreach (var zone in new[] { "Pacific/Kiritimati", "Pacific/Pago_Pago" })
        {
            await SetChildTimeZoneAsync(fixture, guardianToken, child.Id, zone);
            var (expectedDate, _) = LocalNow(zone);

            var checkIn = await CheckInAsync(fixture, childToken, new { Zone = "Green" });

            Assert.Equal(expectedDate, checkIn!.Date);
            Assert.Equal(expectedDate, (await GetTodayAsync(fixture, childToken))!.Date);
        }
    }

    [Fact]
    public async Task A_guardian_calling_the_child_routes_gets_not_found_and_no_journal_is_started()
    {
        var (_, guardianToken, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Emil");

        await CheckInAsync(fixture, guardianToken, new { Zone = "Green" }, expectedStatus: 404);
        await GetTodayAsync(fixture, guardianToken, expectedStatus: 404);
        await UndoAsync(fixture, guardianToken, Guid.CreateVersion7(), expectedStatus: 404);

        var journals = fixture.Host.Services.GetRequiredService<IFeelingsJournalEventStore>();
        Assert.Empty(await journals.ReadAsync(FeelingsJournalId.ForChild(new UserId(guardianId)), CancellationToken.None));
    }

    [Fact]
    public async Task A_child_whose_last_guardian_link_is_gone_gets_not_found()
    {
        var family = await WorkLocations.WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, family.Child);
        await CheckInAsync(fixture, childToken, new { Zone = "Green" });

        foreach (var token in new[] { family.FirstToken, family.SecondToken })
        {
            await fixture.Host.Scenario(_ =>
            {
                _.WithRequestHeader("Authorization", $"Bearer {token}");
                _.Delete.Url($"/users/me/children/{family.Child.Id}/guardian-link");
                _.StatusCodeShouldBe(204);
            });
        }

        await CheckInAsync(fixture, childToken, new { Zone = "Green" }, expectedStatus: 404);
        await GetTodayAsync(fixture, childToken, expectedStatus: 404);
    }

    [Fact]
    public async Task A_feeling_word_from_another_zone_too_many_tags_or_a_long_note_are_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Emil");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        await CheckInAsync(fixture, childToken, new { Zone = "Green", Tags = new[] { "Angry" } }, expectedStatus: 400);
        await CheckInAsync(fixture, childToken, new { Zone = "Green", Tags = new[] { "Happy", "School", "Home", "Friends" } }, expectedStatus: 400);
        await CheckInAsync(fixture, childToken, new { Zone = "Green", Tags = new[] { "School", "School" } }, expectedStatus: 400);
        await CheckInAsync(fixture, childToken, new { Zone = "Green", Note = new string('x', FeelingsRules.MaxNoteLength + 1) }, expectedStatus: 400);
        await CheckInAsync(fixture, childToken, new { Zone = "Purple" }, expectedStatus: 400);
        await CheckInAsync(fixture, childToken, new { Zone = 7 }, expectedStatus: 400);

        // Situation tags fit any zone, and a note of exactly 280 characters is fine.
        await CheckInAsync(fixture, childToken, new { Zone = "Red", Tags = new[] { "Angry", "School", "Screens" }, Note = new string('x', FeelingsRules.MaxNoteLength) });
    }

    [Fact]
    public async Task The_21st_check_in_of_a_day_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Emil");
        await SetChildTimeZoneAsync(fixture, guardianToken, child.Id, Copenhagen);
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        for (var i = 0; i < FeelingsRules.MaxCheckInsPerDay; i++)
        {
            await CheckInAsync(fixture, childToken, new { Zone = "Green" });
        }

        await CheckInAsync(fixture, childToken, new { Zone = "Green" }, expectedStatus: 400);
        await LogAsync(fixture, guardianToken, child.Id, new { Zone = "Green" }, expectedStatus: 400);

        // Another day still has room.
        var (today, _) = LocalNow(Copenhagen);
        await LogAsync(fixture, guardianToken, child.Id, new { Zone = "Green", Date = today.AddDays(-1), Time = "12:00" });
    }

    [Fact]
    [CoversEndpoint("UndoCheckIn")]
    public async Task A_child_undoes_a_mis_tap_and_undoing_again_is_a_no_op()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Emil");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var checkIn = await CheckInAsync(fixture, childToken, new { Zone = "Red" });

        await UndoAsync(fixture, childToken, checkIn!.Id);
        await UndoAsync(fixture, childToken, checkIn.Id);

        Assert.Empty((await GetTodayAsync(fixture, childToken))!.CheckIns);

        var journals = fixture.Host.Services.GetRequiredService<IFeelingsJournalEventStore>();
        var events = await journals.ReadAsync(FeelingsJournalId.ForChild(new UserId(child.Id)), CancellationToken.None);
        var removed = Assert.Single(events.Select(e => e.Value).OfType<FeelingCheckInRemoved>());
        Assert.Equal(child.Id, removed.ModifiedBy.Value);
    }

    [Fact]
    public async Task A_child_cannot_undo_a_guardians_observation_or_a_siblings_check_in()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var emil = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Emil");
        var ida = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Ida");
        var emilToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, emil);
        var idaToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, ida);
        var observation = await LogAsync(fixture, guardianToken, emil.Id, new { Zone = "Red" });
        var idasCheckIn = await CheckInAsync(fixture, idaToken, new { Zone = "Blue" });

        await UndoAsync(fixture, emilToken, observation!.Id, expectedStatus: 404);
        // Ida's check-in lives in Ida's journal; from Emil's /me it doesn't exist, so it's a no-op.
        await UndoAsync(fixture, emilToken, idasCheckIn!.Id);

        Assert.Single((await GetTodayAsync(fixture, idaToken))!.CheckIns);
        Assert.Single((await ListAsync(fixture, guardianToken, emil.Id, observation.Date, observation.Date))!.CheckIns);
    }
}
