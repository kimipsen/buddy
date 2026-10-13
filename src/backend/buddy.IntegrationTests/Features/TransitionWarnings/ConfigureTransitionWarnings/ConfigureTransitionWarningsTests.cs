using buddy.Common;
using buddy.Features.TransitionWarnings;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.TransitionWarnings.TransitionWarningsTestHelpers;

namespace buddy.IntegrationTests.Features.TransitionWarnings.ConfigureTransitionWarnings;

[Collection(BuddyApiCollection.Name)]
public sealed class ConfigureTransitionWarningsTests(BuddyApiFixture fixture)
{
    private async Task<IReadOnlyCollection<TransitionWarningEvent>> EventsAsync(Guid childId) =>
        await fixture.Host.Services.GetRequiredService<ITransitionWarningSettingsEventStore>()
            .ReadAsync(new TransitionWarningSettingsId(childId), CancellationToken.None);

    private async Task<ErrorEnvelope> ConfigureInvalidAsync(WarningFamily family, object body)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {family.GuardianToken}");
            _.Put.Json(body).ToUrl($"/transition-warnings/children/{family.Child.Id}");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        return error;
    }

    [Fact]
    [CoversEndpoint("ConfigureTransitionWarnings")]
    public async Task A_first_save_turning_warnings_on_starts_the_stream()
    {
        var family = await CreateFamilyAsync(fixture);

        var saved = await ConfigureAsync(fixture, family.GuardianToken, family.Child.Id,
            Settings(true, [1, 15, 5], [WarningKind.PickUp, WarningKind.Event], new QuietHoursDto(new TimeOnly(21, 0), new TimeOnly(7, 0))));

        Assert.NotNull(saved);
        Assert.True(saved.Enabled);
        // Kept largest first and in kind order, whatever order they were sent in.
        Assert.Equal([15, 5, 1], saved.LeadMinutes);
        Assert.Equal([WarningKind.Event, WarningKind.PickUp], saved.Kinds);
        Assert.Equal(new TimeOnly(21, 0), saved.QuietHours?.From);

        var events = await EventsAsync(family.Child.Id);
        Assert.Collection(events, e => Assert.IsType<TransitionWarningsStarted>(e.Value), e => Assert.IsType<TransitionWarningDefaultsConfigured>(e.Value));

        var read = await GetForChildAsync(fixture, family.GuardianToken, family.Child.Id);
        Assert.Equivalent(saved, read);
    }

    [Fact]
    public async Task An_identical_save_appends_nothing()
    {
        var family = await CreateFamilyAsync(fixture);
        await ConfigureAsync(fixture, family.GuardianToken, family.Child.Id, Settings(true, [10, 5]));

        await ConfigureAsync(fixture, family.GuardianToken, family.Child.Id, Settings(true, [5, 10]));

        Assert.Equal(2, (await EventsAsync(family.Child.Id)).Count);
    }

    [Fact]
    public async Task Saving_the_initial_settings_on_a_child_with_no_stream_appends_nothing()
    {
        var family = await CreateFamilyAsync(fixture);

        await ConfigureAsync(fixture, family.GuardianToken, family.Child.Id, Settings(false, [15, 5, 1]));

        Assert.Empty(await EventsAsync(family.Child.Id));
    }

    [Fact]
    public async Task Turning_warnings_off_keeps_the_lead_times_kinds_quiet_hours_and_overrides()
    {
        var family = await CreateFamilyAsync(fixture);
        var quiet = new QuietHoursDto(new TimeOnly(8, 0), new TimeOnly(14, 0));
        await ConfigureAsync(fixture, family.GuardianToken, family.Child.Id, Settings(true, [20], [WarningKind.DropOff], quiet));
        await SetOverrideAsync(fixture, family.GuardianToken, family.Child.Id, family.Event.Id, [30]);

        var off = await ConfigureAsync(fixture, family.GuardianToken, family.Child.Id, Settings(false, [20], [WarningKind.DropOff], quiet));

        Assert.NotNull(off);
        Assert.False(off.Enabled);
        Assert.Equal([20], off.LeadMinutes);
        Assert.Equal([WarningKind.DropOff], off.Kinds);
        Assert.Equal(quiet, off.QuietHours);
        Assert.Equal([30], Assert.Single(off.Overrides).LeadMinutes);
    }

    [Fact]
    public async Task Lead_times_must_be_one_to_three_distinct_values_between_1_and_120_minutes()
    {
        var family = await CreateFamilyAsync(fixture);

        foreach (var leadMinutes in new[] { Array.Empty<int>(), [15, 10, 5, 1], [5, 5], [0], [121] })
        {
            var error = await ConfigureInvalidAsync(family, Settings(true, leadMinutes));
            Assert.Contains("LeadMinutes", error.Details.Keys);
        }
    }

    [Fact]
    public async Task Turning_warnings_on_needs_at_least_one_kind_but_off_does_not()
    {
        var family = await CreateFamilyAsync(fixture);

        var error = await ConfigureInvalidAsync(family, Settings(true, [5], []));
        Assert.Contains("Kinds", error.Details.Keys);

        await ConfigureAsync(fixture, family.GuardianToken, family.Child.Id, Settings(false, [5], []));
    }

    [Fact]
    public async Task Quiet_hours_must_start_and_end_at_different_times()
    {
        var family = await CreateFamilyAsync(fixture);

        var error = await ConfigureInvalidAsync(family, Settings(true, [5], quietHours: new QuietHoursDto(new TimeOnly(9, 0), new TimeOnly(9, 0))));

        Assert.Contains("QuietHours", error.Details.Keys);
    }

    [Fact]
    public async Task An_unknown_kind_is_rejected()
    {
        var family = await CreateFamilyAsync(fixture);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {family.GuardianToken}");
            _.Put.Json(new { Enabled = true, LeadMinutes = new[] { 5 }, Kinds = new[] { "Bedtime" } }).ToUrl($"/transition-warnings/children/{family.Child.Id}");
            _.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task The_child_cannot_change_their_own_settings()
    {
        var family = await CreateFamilyAsync(fixture);
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, family.Child);

        await ConfigureAsync(fixture, childToken, family.Child.Id, Settings(false, [5]), expectedStatus: 403);
    }

    [Fact]
    public async Task A_guardian_of_another_family_gets_not_found()
    {
        var family = await CreateFamilyAsync(fixture);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await ConfigureAsync(fixture, strangerToken, family.Child.Id, Settings(true, [5]), expectedStatus: 404);
    }

    [Fact]
    public async Task Erasing_the_child_deletes_their_settings()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Ida");
        await ConfigureAsync(fixture, token, child.Id, Settings(true, [5]));

        // The child's only guardian deletes their account, which erases the child too.
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url("/users/me");
            _.StatusCodeShouldBe(204);
        });

        var store = fixture.Host.Services.GetRequiredService<ITransitionWarningSettingsEventStore>();
        Assert.Empty(await EventsAsync(child.Id));
        Assert.Null(await store.FindSnapshotAsync(new TransitionWarningSettingsId(child.Id), CancellationToken.None));
    }
}
