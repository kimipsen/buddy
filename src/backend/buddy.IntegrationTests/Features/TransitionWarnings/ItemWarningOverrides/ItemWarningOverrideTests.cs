using buddy.Common;
using buddy.Features.Calendars;
using buddy.Features.TransitionWarnings;
using buddy.IntegrationTests.Features.Calendars;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.TransitionWarnings.TransitionWarningsTestHelpers;

namespace buddy.IntegrationTests.Features.TransitionWarnings.ItemWarningOverrides;

[Collection(BuddyApiCollection.Name)]
public sealed class ItemWarningOverrideTests(BuddyApiFixture fixture)
{
    private async Task<IReadOnlyCollection<TransitionWarningEvent>> EventsAsync(Guid childId) =>
        await fixture.Host.Services.GetRequiredService<ITransitionWarningSettingsEventStore>()
            .ReadAsync(new TransitionWarningSettingsId(childId), CancellationToken.None);

    [Fact]
    [CoversEndpoint("SetItemWarningOverride")]
    public async Task A_guardian_gives_an_item_its_own_lead_times_and_sees_its_title_and_icon()
    {
        var family = await CreateFamilyAsync(fixture, "Football");

        var response = await SetOverrideAsync(fixture, family.GuardianToken, family.Child.Id, family.Event.Id, [10, 30]);

        var settings = response.ReadAsJson<ChildTransitionWarningsDto>();
        var single = Assert.Single(settings.Overrides);
        Assert.Equal(family.Event.Id, single.ItemId);
        Assert.Equal("Football", single.Title);
        Assert.Equal("calendar", single.Icon);
        Assert.Equal(CalendarItemKind.Event, single.Kind);
        Assert.Equal([30, 10], single.LeadMinutes);
        // The first write starts the stream, warnings still off.
        Assert.False(settings.Enabled);
        Assert.Collection(await EventsAsync(family.Child.Id), e => Assert.IsType<TransitionWarningsStarted>(e.Value), e => Assert.IsType<ItemWarningOverrideSet>(e.Value));
    }

    [Fact]
    public async Task An_empty_list_means_never_warn_and_an_identical_override_appends_nothing()
    {
        var family = await CreateFamilyAsync(fixture);
        await SetOverrideAsync(fixture, family.GuardianToken, family.Child.Id, family.Event.Id, [10]);

        var off = (await SetOverrideAsync(fixture, family.GuardianToken, family.Child.Id, family.Event.Id, [])).ReadAsJson<ChildTransitionWarningsDto>();
        await SetOverrideAsync(fixture, family.GuardianToken, family.Child.Id, family.Event.Id, []);

        Assert.Empty(Assert.Single(off.Overrides).LeadMinutes);
        var events = await EventsAsync(family.Child.Id);
        Assert.Equal(3, events.Count);
        var last = Assert.IsType<ItemWarningOverrideSet>(events.Last().Value);
        Assert.IsType<ItemWarningOverride.LeadTimes>(last.Before);
        Assert.IsType<ItemWarningOverride.Off>(last.After);
    }

    [Fact]
    public async Task An_override_is_validated_like_the_defaults()
    {
        var family = await CreateFamilyAsync(fixture);

        foreach (var leadMinutes in new[] { new[] { 15, 10, 5, 1 }, [5, 5], [0], [121] })
        {
            var response = await SetOverrideAsync(fixture, family.GuardianToken, family.Child.Id, family.Event.Id, leadMinutes, expectedStatus: 400);
            Assert.Contains("LeadMinutes", response.ReadAsJson<ErrorEnvelope>().Details.Keys);
        }
    }

    [Fact]
    public async Task An_item_in_a_calendar_the_guardian_cannot_see_is_not_found()
    {
        var family = await CreateFamilyAsync(fixture);
        var (_, otherToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var otherCalendar = await CalendarTestHelpers.CreateCalendarAsync(fixture, otherToken, "Private");
        var privateEvent = await CalendarTestHelpers.CreateEventAsync(fixture, otherToken, otherCalendar, "Secret");
        Assert.NotNull(privateEvent);

        await SetOverrideAsync(fixture, family.GuardianToken, family.Child.Id, privateEvent.Id, [5], expectedStatus: 404);
        await SetOverrideAsync(fixture, family.GuardianToken, family.Child.Id, Guid.CreateVersion7(), [5], expectedStatus: 404);
        Assert.Empty(await EventsAsync(family.Child.Id));
    }

    [Fact]
    public async Task The_child_is_forbidden_and_a_stranger_gets_not_found()
    {
        var family = await CreateFamilyAsync(fixture);
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, family.Child);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await SetOverrideAsync(fixture, childToken, family.Child.Id, family.Event.Id, [5], expectedStatus: 403);
        await ClearOverrideAsync(fixture, childToken, family.Child.Id, family.Event.Id, expectedStatus: 403);
        await SetOverrideAsync(fixture, strangerToken, family.Child.Id, family.Event.Id, [5], expectedStatus: 404);
        await ClearOverrideAsync(fixture, strangerToken, family.Child.Id, family.Event.Id, expectedStatus: 404);
    }

    [Fact]
    public async Task A_child_can_have_at_most_100_overrides()
    {
        var family = await CreateFamilyAsync(fixture);
        List<Guid> itemIds = [];
        for (var i = 0; i < LeadTimeRules.MaxOverrides; i++)
        {
            var item = await CalendarTestHelpers.CreateEventAsync(fixture, family.GuardianToken, family.CalendarId, $"Event {i}");
            itemIds.Add(item!.Id);
        }

        // The 100 exceptions are seeded through the store; one request each would only slow the test.
        await SeedOverridesAsync(family, itemIds);

        var response = await SetOverrideAsync(fixture, family.GuardianToken, family.Child.Id, family.Event.Id, [5], expectedStatus: 409);

        Assert.Equal(SetItemWarningOverrideOutcome.TooManyOverridesCode, response.ReadAsJson<ErrorEnvelope>().Code);
        // Changing an existing exception is still allowed at the cap.
        await SetOverrideAsync(fixture, family.GuardianToken, family.Child.Id, itemIds[0], [5]);
    }

    [Fact]
    public async Task Exceptions_for_deleted_items_do_not_count_towards_the_cap()
    {
        var family = await CreateFamilyAsync(fixture);
        // Items that no longer exist: the guardian's list leaves them out, so they couldn't be removed.
        await SeedOverridesAsync(family, [.. Enumerable.Range(0, LeadTimeRules.MaxOverrides).Select(_ => Guid.CreateVersion7())]);

        await SetOverrideAsync(fixture, family.GuardianToken, family.Child.Id, family.Event.Id, [5]);

        var settings = await GetForChildAsync(fixture, family.GuardianToken, family.Child.Id);
        Assert.Equal(family.Event.Id, Assert.Single(settings!.Overrides).ItemId);
    }

    private async Task SeedOverridesAsync(WarningFamily family, IReadOnlyList<Guid> itemIds)
    {
        var store = fixture.Host.Services.GetRequiredService<ITransitionWarningSettingsEventStore>();
        var id = new TransitionWarningSettingsId(family.Child.Id);
        var now = DateTimeOffset.UtcNow;
        var guardian = new buddy.Features.Users.UserId(family.GuardianId);

        List<TransitionWarningEvent> seeded = [new TransitionWarningsStarted(id, new buddy.Features.Users.UserId(family.Child.Id), now)];
        seeded.AddRange(itemIds.Select(itemId =>
            (TransitionWarningEvent)new ItemWarningOverrideSet(id, new CalendarItemId(itemId), null, new ItemWarningOverride.Off(), guardian, now)));
        await store.CreateAsync(id, seeded, CancellationToken.None);
    }

    [Fact]
    [CoversEndpoint("ClearItemWarningOverride")]
    public async Task Clearing_an_override_removes_it_and_clearing_again_is_a_no_op()
    {
        var family = await CreateFamilyAsync(fixture);
        await SetOverrideAsync(fixture, family.GuardianToken, family.Child.Id, family.Event.Id, [10]);

        await ClearOverrideAsync(fixture, family.GuardianToken, family.Child.Id, family.Event.Id);
        await ClearOverrideAsync(fixture, family.GuardianToken, family.Child.Id, family.Event.Id);

        var settings = await GetForChildAsync(fixture, family.GuardianToken, family.Child.Id);
        Assert.NotNull(settings);
        Assert.Empty(settings.Overrides);
        Assert.IsType<ItemWarningOverrideCleared>((await EventsAsync(family.Child.Id)).Last().Value);
        Assert.Equal(3, (await EventsAsync(family.Child.Id)).Count);
    }

    [Fact]
    public async Task Clearing_on_a_child_with_no_settings_starts_nothing()
    {
        var family = await CreateFamilyAsync(fixture);

        await ClearOverrideAsync(fixture, family.GuardianToken, family.Child.Id, family.Event.Id);

        Assert.Empty(await EventsAsync(family.Child.Id));
    }
}
