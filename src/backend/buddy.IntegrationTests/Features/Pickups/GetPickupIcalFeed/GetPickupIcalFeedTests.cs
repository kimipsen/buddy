using Alba;

using buddy.Common.Erasure;
using buddy.Features.Pickups;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.WorkLocations;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.Features.Pickups.GetPickupIcalFeed;

[Collection(BuddyApiCollection.Name)]
public sealed class GetPickupIcalFeedTests(BuddyApiFixture fixture)
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);
    private static readonly DateOnly NextWeek = Today.AddDays(7);

    [Fact]
    [CoversEndpoint("GetPickupIcalFeed")]
    public async Task The_feed_holds_the_guardians_own_slots_across_children_in_each_childs_time_zone()
    {
        var (_, guardianToken, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var alex = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var bea = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Bea");
        await SetChildTimeZoneAsync(guardianToken, alex.Id, "Europe/Copenhagen");
        await SetChildTimeZoneAsync(guardianToken, bea.Id, "America/New_York");

        await PickupTestHelpers.AssignAsync(fixture, guardianToken, alex.Id, NextWeek, PickupSlot.DropOff, PickupTestHelpers.Guardian(guardianId), new TimeOnly(7, 45), "Gym bag");
        await PickupTestHelpers.AssignAsync(fixture, guardianToken, bea.Id, NextWeek, PickupSlot.PickUp, PickupTestHelpers.Guardian(guardianId), new TimeOnly(15, 0));

        var issued = await PickupTestHelpers.CreateIcalTokenAsync(fixture, guardianToken);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.Get.Url(issued.SubscriptionPath);
            _.StatusCodeShouldBeOk();
            _.ContentTypeShouldBe("text/calendar");
            _.Header("Cache-Control").SingleValueShouldEqual("private, no-cache");
        });
        var ics = response.ReadAsText();

        Assert.Contains("X-WR-CALNAME:My pickups", ics);
        Assert.Contains("SUMMARY:Drop off Alex", ics);
        Assert.Contains("SUMMARY:Pick up Bea", ics);
        Assert.Contains("DESCRIPTION:Gym bag", ics);
        Assert.Contains($"DTSTART:{Utc(NextWeek, new TimeOnly(7, 45), "Europe/Copenhagen")}", ics);
        Assert.Contains($"DTEND:{Utc(NextWeek, new TimeOnly(8, 0), "Europe/Copenhagen")}", ics);
        Assert.Contains($"DTSTART:{Utc(NextWeek, new TimeOnly(15, 0), "America/New_York")}", ics);
        Assert.Contains($"UID:{alex.Id:N}-{NextWeek:yyyyMMdd}-DropOff@buddy-pickup", ics);
        Assert.Equal(2, Count(ics, "BEGIN:VALARM"));
        Assert.Equal(2, Count(ics, "TRIGGER:-PT30M"));

        // An unchanged feed renders byte-identically, so a calendar app sending the ETag back gets a 304.
        var etag = response.Context.Response.Headers.ETag.ToString();
        Assert.NotEmpty(etag);
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("If-None-Match", etag);
            _.Get.Url(issued.SubscriptionPath);
            _.StatusCodeShouldBe(304);
        });
    }

    [Fact]
    public async Task Slots_someone_else_does_are_left_out()
    {
        var coGuardians = await WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);
        var child = coGuardians.Child;
        var sibling = await GuardianTestHelpers.CreateChildAsync(fixture, coGuardians.FirstToken, "Sam");

        await PickupTestHelpers.AssignAsync(fixture, coGuardians.FirstToken, child.Id, NextWeek, PickupSlot.DropOff, PickupTestHelpers.Guardian(coGuardians.SecondId));
        await PickupTestHelpers.AssignAsync(fixture, coGuardians.FirstToken, child.Id, NextWeek, PickupSlot.PickUp, PickupTestHelpers.SelfEscort());
        await PickupTestHelpers.AssignAsync(fixture, coGuardians.FirstToken, child.Id, NextWeek.AddDays(1), PickupSlot.PickUp, PickupTestHelpers.Playdate("Mia's mom"));
        await PickupTestHelpers.AssignAsync(fixture, coGuardians.FirstToken, child.Id, NextWeek.AddDays(2), PickupSlot.PickUp,
            new { Kind = (int)PickupAssigneeKind.Sibling, SiblingChildId = sibling.Id });
        await PickupTestHelpers.AssignAsync(fixture, coGuardians.FirstToken, child.Id, NextWeek.AddDays(3), PickupSlot.PickUp, PickupTestHelpers.Guardian(coGuardians.FirstId));

        var firstFeed = await PickupTestHelpers.ReadFeedAsync(fixture, (await PickupTestHelpers.CreateIcalTokenAsync(fixture, coGuardians.FirstToken)).SubscriptionPath);
        var secondFeed = await PickupTestHelpers.ReadFeedAsync(fixture, (await PickupTestHelpers.CreateIcalTokenAsync(fixture, coGuardians.SecondToken)).SubscriptionPath);

        Assert.Equal(1, Count(firstFeed, "BEGIN:VEVENT"));
        Assert.Contains($"{child.Id:N}-{NextWeek.AddDays(3):yyyyMMdd}-PickUp@buddy-pickup", firstFeed);
        Assert.Equal(1, Count(secondFeed, "BEGIN:VEVENT"));
        Assert.Contains($"{child.Id:N}-{NextWeek:yyyyMMdd}-DropOff@buddy-pickup", secondFeed);
    }

    [Fact]
    public async Task A_slot_without_a_time_is_an_all_day_event_without_an_alarm()
    {
        var (_, guardianToken, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        await PickupTestHelpers.AssignAsync(fixture, guardianToken, child.Id, NextWeek, PickupSlot.PickUp, PickupTestHelpers.Guardian(guardianId));

        var ics = await PickupTestHelpers.ReadFeedAsync(fixture, (await PickupTestHelpers.CreateIcalTokenAsync(fixture, guardianToken)).SubscriptionPath);

        Assert.Contains($"DTSTART;VALUE=DATE:{NextWeek:yyyyMMdd}", ics);
        Assert.Contains($"DTEND;VALUE=DATE:{NextWeek.AddDays(1):yyyyMMdd}", ics);
        Assert.DoesNotContain("BEGIN:VALARM", ics);
        Assert.DoesNotContain("DESCRIPTION", ics);
    }

    [Fact]
    public async Task A_danish_guardian_gets_danish_titles()
    {
        var (_, guardianToken, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Patch.Json(new { Language = "da" }).ToUrl("/users/me/language");
            _.StatusCodeShouldBeOk();
        });
        await PickupTestHelpers.AssignAsync(fixture, guardianToken, child.Id, NextWeek, PickupSlot.DropOff, PickupTestHelpers.Guardian(guardianId));
        await PickupTestHelpers.AssignAsync(fixture, guardianToken, child.Id, NextWeek, PickupSlot.PickUp, PickupTestHelpers.Guardian(guardianId));

        var ics = await PickupTestHelpers.ReadFeedAsync(fixture, (await PickupTestHelpers.CreateIcalTokenAsync(fixture, guardianToken)).SubscriptionPath);

        Assert.Contains("X-WR-CALNAME:Mine afhentninger", ics);
        Assert.Contains("SUMMARY:Aflever Alex", ics);
        Assert.Contains("SUMMARY:Hent Alex", ics);
    }

    [Fact]
    public async Task Slots_outside_the_window_are_left_out()
    {
        var (_, guardianToken, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        foreach (var date in new[] { Today.AddDays(-15), Today.AddDays(-14), Today.AddDays(60), Today.AddDays(61) })
        {
            await PickupTestHelpers.AssignAsync(fixture, guardianToken, child.Id, date, PickupSlot.PickUp, PickupTestHelpers.Guardian(guardianId));
        }

        var ics = await PickupTestHelpers.ReadFeedAsync(fixture, (await PickupTestHelpers.CreateIcalTokenAsync(fixture, guardianToken)).SubscriptionPath);

        Assert.Equal(2, Count(ics, "BEGIN:VEVENT"));
        Assert.Contains($"-{Today.AddDays(-14):yyyyMMdd}-PickUp@", ics);
        Assert.Contains($"-{Today.AddDays(60):yyyyMMdd}-PickUp@", ics);
    }

    [Fact]
    public async Task A_child_the_guardian_is_unlinked_from_leaves_the_feed()
    {
        var coGuardians = await WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);
        await PickupTestHelpers.AssignAsync(fixture, coGuardians.FirstToken, coGuardians.Child.Id, NextWeek, PickupSlot.PickUp, PickupTestHelpers.Guardian(coGuardians.SecondId));
        var issued = await PickupTestHelpers.CreateIcalTokenAsync(fixture, coGuardians.SecondToken);
        Assert.Contains("BEGIN:VEVENT", await PickupTestHelpers.ReadFeedAsync(fixture, issued.SubscriptionPath));

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {coGuardians.SecondToken}");
            _.Delete.Url($"/users/me/children/{coGuardians.Child.Id}/guardian-link");
            _.StatusCodeShouldBe(204);
        });

        Assert.DoesNotContain("BEGIN:VEVENT", await PickupTestHelpers.ReadFeedAsync(fixture, issued.SubscriptionPath));
    }

    [Fact]
    public async Task A_wrong_token_or_an_unknown_feed_returns_not_found()
    {
        var (_, guardianToken, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var issued = await PickupTestHelpers.CreateIcalTokenAsync(fixture, guardianToken);

        await fixture.Host.Scenario(_ =>
        {
            _.Get.Url($"/pickups/feeds/{guardianId}/ical/not-a-real-token");
            _.StatusCodeShouldBe(404);
        });
        await fixture.Host.Scenario(_ =>
        {
            _.Get.Url($"/pickups/feeds/{Guid.CreateVersion7()}/ical/{issued.Token}");
            _.StatusCodeShouldBe(404);
        });
    }

    [Fact]
    public async Task Erasing_the_guardian_deletes_the_feed()
    {
        var (_, guardianToken, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var issued = await PickupTestHelpers.CreateIcalTokenAsync(fixture, guardianToken);
        var eraser = fixture.Host.Services.GetServices<IPersonalDataEraser>().OfType<PickupsPersonalDataEraser>().Single();

        await eraser.EraseGuardianAsync(new ErasureSubject(new UserId(guardianId), null), CancellationToken.None);

        await fixture.Host.Scenario(_ =>
        {
            _.Get.Url(issued.SubscriptionPath);
            _.StatusCodeShouldBe(404);
        });
        var feeds = fixture.Host.Services.GetRequiredService<IPickupFeedEventStore>();
        Assert.Empty(await feeds.ReadAsync(PickupFeedId.ForGuardian(new UserId(guardianId)), CancellationToken.None));
    }

    private Task<IScenarioResult> SetChildTimeZoneAsync(string guardianToken, Guid childId, string zone) =>
        fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Patch.Json(new { TimeZoneId = zone }).ToUrl($"/users/me/children/{childId}/timezone");
            _.StatusCodeShouldBeOk();
        });

    private static string Utc(DateOnly date, TimeOnly time, string zone)
    {
        var local = date.ToDateTime(time);
        var utc = TimeZoneInfo.ConvertTimeToUtc(local, TimeZoneInfo.FindSystemTimeZoneById(zone));

        return utc.ToString("yyyyMMdd'T'HHmmss'Z'", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static int Count(string text, string value) => text.Split(value).Length - 1;
}
