using System.Text.Json;

using buddy.Features.Feelings;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.WorkLocations;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.Feelings.FeelingsTestHelpers;

namespace buddy.IntegrationTests.Features.Feelings.GuardianCheckIns;

// A guardian's routes on a child's journal: LogFeelingCheckIn, EditFeelingCheckIn,
// RemoveFeelingCheckIn and ListFeelingCheckIns.
[Collection(BuddyApiCollection.Name)]
public sealed class GuardianCheckInTests(BuddyApiFixture fixture)
{
    private const string Copenhagen = "Europe/Copenhagen";
    private static readonly DateOnly LastMonday = new(2026, 3, 2);

    [Fact]
    [CoversEndpoint("LogFeelingCheckIn")]
    [CoversEndpoint("ListFeelingCheckIns")]
    public async Task A_guardian_logs_an_observation_and_lists_it_with_the_childs_own()
    {
        var (_, guardianToken, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Emil");
        await SetChildTimeZoneAsync(fixture, guardianToken, child.Id, Copenhagen);
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var (today, _) = LocalNow(Copenhagen);
        var past = today.AddDays(-3);

        var own = await CheckInAsync(fixture, childToken, new { Zone = "Green", Tags = new[] { "Happy" } });
        var observation = await LogAsync(fixture, guardianToken, child.Id, new { Zone = "Red", Tags = new[] { "Overwhelmed", "School" }, Note = "At pickup", Date = past, Time = "15:10" });
        var now = await LogAsync(fixture, guardianToken, child.Id, new { Zone = "Blue" });

        Assert.Equal(past, observation!.Date);
        Assert.Equal(new TimeOnly(15, 10), observation.Time);
        Assert.Equal(guardianId, observation.LoggedBy);
        Assert.False(observation.IsSelfReport);
        Assert.Equal(today, now!.Date);

        var listed = await ListAsync(fixture, guardianToken, child.Id, past, today);

        // Sorted by the moment described, then by when it was saved.
        Assert.Equal([observation.Id, own!.Id, now.Id], listed!.CheckIns.Select(c => c.Id));
        Assert.Equal(past, listed.From);
        Assert.Equal(today, listed.To);
        Assert.True(Assert.Single(listed.CheckIns, c => c.Id == own.Id).IsSelfReport);

        var onlyMonday = await ListAsync(fixture, guardianToken, child.Id, past, past);
        Assert.Equal(observation.Id, Assert.Single(onlyMonday!.CheckIns).Id);
    }

    [Fact]
    public async Task A_guardian_cannot_post_date_beyond_five_minutes_of_slack_and_must_give_both_date_and_time()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Emil");
        await SetChildTimeZoneAsync(fixture, guardianToken, child.Id, Copenhagen);
        var (today, _) = LocalNow(Copenhagen);

        await LogAsync(fixture, guardianToken, child.Id, new { Zone = "Green", Date = today.AddDays(1), Time = "00:00" }, expectedStatus: 400);
        await LogAsync(fixture, guardianToken, child.Id, new { Zone = "Green", Date = today }, expectedStatus: 400);
        await LogAsync(fixture, guardianToken, child.Id, new { Zone = "Green", Time = "12:00" }, expectedStatus: 400);
        await LogAsync(fixture, guardianToken, child.Id, new { Zone = "Green", Tags = new[] { "Worried" } }, expectedStatus: 400);

        // No lower bound: a week ago is fine.
        await LogAsync(fixture, guardianToken, child.Id, new { Zone = "Green", Date = today.AddDays(-7), Time = "08:00" });
    }

    [Fact]
    [CoversEndpoint("EditFeelingCheckIn")]
    public async Task A_guardian_edits_an_observation_and_an_identical_edit_appends_nothing()
    {
        var family = await WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);
        var observation = await LogAsync(fixture, family.FirstToken, family.Child.Id, new { Zone = "Red", Date = LastMonday, Time = "15:10" });
        var body = new { Zone = "Yellow", Tags = new[] { "Frustrated" }, Note = "Calmer than I wrote", Date = LastMonday, Time = "15:30" };

        // Either guardian may correct it, as with a sleep diary night.
        var edited = await EditAsync(fixture, family.SecondToken, family.Child.Id, observation!.Id, body);
        await EditAsync(fixture, family.SecondToken, family.Child.Id, observation.Id, body);

        Assert.Equal(observation.Id, edited!.Id);
        Assert.Equal("Yellow", edited.Zone);
        Assert.Equal(["Frustrated"], edited.Tags);
        Assert.Equal(new TimeOnly(15, 30), edited.Time);
        Assert.Equal(family.FirstId, edited.LoggedBy);
        Assert.Equal(observation.RecordedAt, edited.RecordedAt);

        var journals = fixture.Host.Services.GetRequiredService<IFeelingsJournalEventStore>();
        var events = await journals.ReadAsync(FeelingsJournalId.ForChild(new UserId(family.Child.Id)), CancellationToken.None);
        var edit = Assert.Single(events.Select(e => e.Value).OfType<FeelingCheckInEdited>());
        Assert.Equal(family.SecondId, edit.ModifiedBy.Value);
        Assert.Equal(FeelingZone.Red, edit.Before.Zone);
    }

    [Fact]
    public async Task A_childs_self_report_cannot_be_edited_but_can_be_removed_and_the_event_names_the_guardian()
    {
        var (_, guardianToken, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Emil");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var own = await CheckInAsync(fixture, childToken, new { Zone = "Red" });

        var response = await EditRawAsync(fixture, guardianToken, child.Id, own!.Id, new { Zone = "Green", Date = own.Date, Time = own.Time }, expectedStatus: 409);
        using (var json = JsonDocument.Parse(await response.ReadAsTextAsync()))
        {
            Assert.Equal(EditFeelingCheckInOutcome.IsSelfReportCode, json.RootElement.GetProperty("code").GetString());
        }

        await RemoveAsync(fixture, guardianToken, child.Id, own.Id);
        await RemoveAsync(fixture, guardianToken, child.Id, own.Id);

        Assert.Empty((await GetTodayAsync(fixture, childToken))!.CheckIns);

        var journals = fixture.Host.Services.GetRequiredService<IFeelingsJournalEventStore>();
        var events = await journals.ReadAsync(FeelingsJournalId.ForChild(new UserId(child.Id)), CancellationToken.None);
        Assert.Equal(guardianId, Assert.Single(events.Select(e => e.Value).OfType<FeelingCheckInRemoved>()).ModifiedBy.Value);
    }

    [Fact]
    [CoversEndpoint("RemoveFeelingCheckIn")]
    public async Task A_guardian_removes_an_observation()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Emil");
        var observation = await LogAsync(fixture, guardianToken, child.Id, new { Zone = "Blue", Date = LastMonday, Time = "07:00" });

        await RemoveAsync(fixture, guardianToken, child.Id, observation!.Id);

        Assert.Empty((await ListAsync(fixture, guardianToken, child.Id, LastMonday, LastMonday))!.CheckIns);
    }

    [Fact]
    public async Task Editing_an_unknown_check_in_or_into_the_future_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Emil");
        await SetChildTimeZoneAsync(fixture, guardianToken, child.Id, Copenhagen);
        var (today, _) = LocalNow(Copenhagen);
        var observation = await LogAsync(fixture, guardianToken, child.Id, new { Zone = "Blue", Date = LastMonday, Time = "07:00" });

        await EditAsync(fixture, guardianToken, child.Id, Guid.CreateVersion7(), new { Zone = "Green", Date = LastMonday, Time = "07:00" }, expectedStatus: 404);
        await EditAsync(fixture, guardianToken, child.Id, observation!.Id, new { Zone = "Green", Date = today.AddDays(1), Time = "07:00" }, expectedStatus: 400);
        await EditAsync(fixture, guardianToken, child.Id, observation.Id, new { Zone = "Green", Tags = new[] { "Sad" }, Date = LastMonday, Time = "07:00" }, expectedStatus: 400);
    }

    [Fact]
    public async Task A_range_over_92_days_or_backwards_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Emil");

        await ListAsync(fixture, guardianToken, child.Id, LastMonday, LastMonday.AddDays(FeelingsRules.MaxRangeDays));
        await ListAsync(fixture, guardianToken, child.Id, LastMonday, LastMonday.AddDays(FeelingsRules.MaxRangeDays + 1), expectedStatus: 400);
        await ListAsync(fixture, guardianToken, child.Id, LastMonday, LastMonday.AddDays(-1), expectedStatus: 400);
    }

    [Fact]
    public async Task The_child_gets_forbidden_and_strangers_and_unrelated_children_get_not_found_on_guardian_routes()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Emil");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var (_, otherGuardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var unrelatedChild = await GuardianTestHelpers.CreateChildAsync(fixture, otherGuardianToken, "Sam");
        var unrelatedChildToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, unrelatedChild);
        var observation = await LogAsync(fixture, guardianToken, child.Id, new { Zone = "Blue", Date = LastMonday, Time = "07:00" });
        var edit = new { Zone = "Green", Date = LastMonday, Time = "07:00" };

        await LogAsync(fixture, childToken, child.Id, new { Zone = "Green" }, expectedStatus: 403);
        await EditAsync(fixture, childToken, child.Id, observation!.Id, edit, expectedStatus: 403);
        await RemoveAsync(fixture, childToken, child.Id, observation.Id, expectedStatus: 403);
        await ListAsync(fixture, childToken, child.Id, LastMonday, LastMonday, expectedStatus: 403);

        foreach (var other in new[] { strangerToken, unrelatedChildToken })
        {
            await LogAsync(fixture, other, child.Id, new { Zone = "Green" }, expectedStatus: 404);
            await EditAsync(fixture, other, child.Id, observation.Id, edit, expectedStatus: 404);
            await RemoveAsync(fixture, other, child.Id, observation.Id, expectedStatus: 404);
            await ListAsync(fixture, other, child.Id, LastMonday, LastMonday, expectedStatus: 404);
        }

        Assert.Single((await ListAsync(fixture, guardianToken, child.Id, LastMonday, LastMonday))!.CheckIns);
    }

    [Fact]
    public async Task A_revoked_guardian_drops_to_not_found_and_the_check_ins_stay()
    {
        var family = await WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);
        await LogAsync(fixture, family.SecondToken, family.Child.Id, new { Zone = "Blue", Date = LastMonday, Time = "07:00" });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {family.SecondToken}");
            _.Delete.Url($"/users/me/children/{family.Child.Id}/guardian-link");
            _.StatusCodeShouldBe(204);
        });

        await ListAsync(fixture, family.SecondToken, family.Child.Id, LastMonday, LastMonday, expectedStatus: 404);
        Assert.Single((await ListAsync(fixture, family.FirstToken, family.Child.Id, LastMonday, LastMonday))!.CheckIns);
    }
}
