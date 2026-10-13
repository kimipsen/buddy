using buddy.Features.Feelings;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.Feelings.FeelingsTestHelpers;

namespace buddy.IntegrationTests.Features.Feelings.Settings;

// The child's two check-in settings on the journal: SetCheckInPromptTimes (Question 6) and
// SetFeelingsDisplayMode, the faces-only mode (Question 9).
[Collection(BuddyApiCollection.Name)]
public sealed class FeelingsSettingsTests(BuddyApiFixture fixture)
{
    private static readonly DateOnly Day = new(2026, 3, 2);

    [Fact]
    [CoversEndpoint("SetCheckInPromptTimes")]
    public async Task Prompt_times_are_stored_sorted_and_the_child_and_guardian_both_see_them()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Emil");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        var set = await SetPromptTimesAsync(fixture, guardianToken, child.Id, ["19:30", "07:30", "15:00"]);

        TimeOnly[] expected = [new(7, 30), new(15, 0), new(19, 30)];
        Assert.Equal(expected, set!.Times);
        Assert.Equal(expected, (await GetTodayAsync(fixture, childToken))!.PromptTimes);
        Assert.Equal(expected, (await ListAsync(fixture, guardianToken, child.Id, Day, Day))!.PromptTimes);

        Assert.Empty((await SetPromptTimesAsync(fixture, guardianToken, child.Id, []))!.Times);
        Assert.Empty((await GetTodayAsync(fixture, childToken))!.PromptTimes);
    }

    [Fact]
    public async Task Setting_prompt_times_before_any_check_in_starts_the_journal_and_the_same_times_append_nothing()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Emil");

        await SetPromptTimesAsync(fixture, guardianToken, child.Id, ["07:30", "15:00"]);
        await SetPromptTimesAsync(fixture, guardianToken, child.Id, ["15:00", "07:30"]);

        var journals = fixture.Host.Services.GetRequiredService<IFeelingsJournalEventStore>();
        var events = await journals.ReadAsync(FeelingsJournalId.ForChild(new UserId(child.Id)), CancellationToken.None);

        Assert.Equal([nameof(FeelingsJournalStarted), nameof(CheckInPromptTimesSet)], events.Select(e => e.EventType));
    }

    [Fact]
    public async Task More_than_four_duplicate_or_part_minute_prompt_times_are_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Emil");

        await SetPromptTimesAsync(fixture, guardianToken, child.Id, ["07:00", "09:00", "12:00", "15:00", "19:00"], expectedStatus: 400);
        await SetPromptTimesAsync(fixture, guardianToken, child.Id, ["07:00", "07:00"], expectedStatus: 400);
        await SetPromptTimesAsync(fixture, guardianToken, child.Id, ["07:00:30"], expectedStatus: 400);
        await SetPromptTimesAsync(fixture, guardianToken, child.Id, ["07:00", "09:00", "12:00", "15:00"]);
    }

    [Fact]
    [CoversEndpoint("SetFeelingsDisplayMode")]
    public async Task Faces_only_reaches_the_child_and_the_guardian_and_the_same_mode_appends_nothing()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Ida");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        // The default on a child with no journal is a no-op that starts nothing.
        Assert.Equal("FacesAndWords", (await SetDisplayModeAsync(fixture, guardianToken, child.Id, "FacesAndWords"))!.DisplayMode);

        Assert.Equal("FacesOnly", (await SetDisplayModeAsync(fixture, guardianToken, child.Id, "FacesOnly"))!.DisplayMode);
        await SetDisplayModeAsync(fixture, guardianToken, child.Id, "FacesOnly");

        Assert.Equal("FacesOnly", (await GetTodayAsync(fixture, childToken))!.DisplayMode);
        Assert.Equal("FacesOnly", (await ListAsync(fixture, guardianToken, child.Id, Day, Day))!.DisplayMode);

        // The stored zones are the same in either mode.
        Assert.Equal("Yellow", (await CheckInAsync(fixture, childToken, new { Zone = "Yellow" }))!.Zone);

        var journals = fixture.Host.Services.GetRequiredService<IFeelingsJournalEventStore>();
        var events = await journals.ReadAsync(FeelingsJournalId.ForChild(new UserId(child.Id)), CancellationToken.None);
        Assert.Equal(
            [nameof(FeelingsJournalStarted), nameof(FeelingsDisplayModeSet), nameof(FeelingCheckedIn)],
            events.Select(e => e.EventType));

        Assert.Equal("FacesAndWords", (await SetDisplayModeAsync(fixture, guardianToken, child.Id, "FacesAndWords"))!.DisplayMode);
        Assert.Equal("FacesAndWords", (await GetTodayAsync(fixture, childToken))!.DisplayMode);
    }

    [Fact]
    public async Task An_unknown_display_mode_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Ida");

        await SetDisplayModeAsync(fixture, guardianToken, child.Id, "WordsOnly", expectedStatus: 400);
        await SetDisplayModeAsync(fixture, guardianToken, child.Id, 5, expectedStatus: 400);
    }

    [Fact]
    public async Task The_child_gets_forbidden_and_a_stranger_not_found_on_the_settings()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Ida");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await SetPromptTimesAsync(fixture, childToken, child.Id, ["07:00"], expectedStatus: 403);
        await SetDisplayModeAsync(fixture, childToken, child.Id, "FacesOnly", expectedStatus: 403);
        await SetPromptTimesAsync(fixture, strangerToken, child.Id, ["07:00"], expectedStatus: 404);
        await SetDisplayModeAsync(fixture, strangerToken, child.Id, "FacesOnly", expectedStatus: 404);
    }
}
