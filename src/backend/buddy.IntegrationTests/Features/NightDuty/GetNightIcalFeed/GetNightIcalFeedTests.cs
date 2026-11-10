using System.Text.Json;

using Alba;

using buddy.Common.Erasure;
using buddy.Features.NightDuty;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Babysitters;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.WorkLocations;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.NightDuty.NightDutyTestHelpers;

namespace buddy.IntegrationTests.Features.NightDuty.GetNightIcalFeed;

[Collection(BuddyApiCollection.Name)]
public sealed class GetNightIcalFeedTests(BuddyApiFixture fixture)
{
    // The UTC date; the feed's window is centred on the child's own date, which is at most a day off.
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    [CoversEndpoint("GetNightIcalFeed")]
    public async Task The_feed_holds_one_all_day_event_per_planned_night()
    {
        var (_, guardianToken, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        await SetCallingNameAsync(guardianToken, "Dad");
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var babysitter = await BabysitterTestHelpers.AddAsync(fixture, guardianToken, "Anna");
        var monday = FutureMonday();

        await ReplacePatternAsync(fixture, guardianToken, child.Id, monday, 1, monday,
            Day(0, DayOfWeek.Monday, Guardian(guardianId, "Screen night")),
            Day(0, DayOfWeek.Tuesday, Babysitter(guardianId, babysitter.Id)),
            Day(0, DayOfWeek.Wednesday, new
            {
                Responsible = new { Kind = ResponsibleSleepover, HostName = "Grandma Lis", Location = "Skovvej 12", ContactInfo = "20 12 34 56" },
            }),
            Day(0, DayOfWeek.Thursday, NoteOnly("Swim bag")),
            Day(0, DayOfWeek.Friday, Guardian(guardianId)));
        // Friday's scheduled night is cleared; Saturday and Sunday have nothing planned.
        await SetOverridesAsync(fixture, guardianToken, child.Id, Override(monday.AddDays(4), null));

        var issued = await CreateIcalTokenAsync(fixture, guardianToken, child.Id);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.Get.Url(issued.SubscriptionPath);
            _.StatusCodeShouldBeOk();
            _.ContentTypeShouldBe("text/calendar");
            _.Header("Cache-Control").SingleValueShouldEqual("private, no-cache");
        });
        var ics = response.ReadAsText();

        Assert.Contains("X-WR-CALNAME:Night duty: Alex", ics);
        // The weekly pattern repeats through the window; this week's Monday to Thursday are planned.
        Assert.All(Enumerable.Range(0, 4), offset => Assert.Contains($"{monday.AddDays(offset):yyyyMMdd}@buddy-night", ics));
        Assert.Contains("SUMMARY:Night: Dad · Screen night", ics);
        Assert.Contains("SUMMARY:Night: Anna", ics);
        Assert.Contains("SUMMARY:Sleepover: Grandma Lis", ics);
        Assert.Contains("LOCATION:Skovvej 12", ics);
        Assert.Contains("DESCRIPTION:20 12 34 56", ics);
        Assert.Contains("SUMMARY:Night: Swim bag", ics);
        Assert.Contains($"UID:{child.Id:N}-{monday:yyyyMMdd}@buddy-night", ics);
        Assert.Contains($"DTSTART;VALUE=DATE:{monday:yyyyMMdd}", ics);
        Assert.Contains($"DTEND;VALUE=DATE:{monday.AddDays(1):yyyyMMdd}", ics);
        Assert.DoesNotContain($"{monday.AddDays(4):yyyyMMdd}@buddy-night", ics);
        Assert.DoesNotContain($"{monday.AddDays(5):yyyyMMdd}@buddy-night", ics);
        Assert.DoesNotContain("BEGIN:VALARM", ics);

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
    public async Task An_override_changes_the_night_under_the_same_uid()
    {
        var coGuardians = await WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);
        var child = coGuardians.Child;
        var monday = FutureMonday();
        await ReplacePatternAsync(fixture, coGuardians.FirstToken, child.Id, monday, 1, monday,
            Day(0, DayOfWeek.Monday, Guardian(coGuardians.FirstId, "Screen night")));
        var issued = await CreateIcalTokenAsync(fixture, coGuardians.SecondToken, child.Id);
        // The first guardian has a calling name; the second has none, so their given name is used.
        await SetCallingNameAsync(coGuardians.FirstToken, "Mom");
        const string firstName = "Mom";
        var secondName = await GivenNameAsync(coGuardians.SecondToken);

        Assert.Contains($"SUMMARY:Night: {firstName} · Screen night", await ReadFeedAsync(fixture, issued.SubscriptionPath));

        await SetOverridesAsync(fixture, coGuardians.FirstToken, child.Id, Override(monday, Guardian(coGuardians.SecondId, "Screen night")));
        var ics = await ReadFeedAsync(fixture, issued.SubscriptionPath);

        // One event for that date, still under its UID, now naming the second guardian; the following
        // Mondays still follow the pattern.
        Assert.Equal(1, Count(ics, $"UID:{child.Id:N}-{monday:yyyyMMdd}@buddy-night"));
        Assert.Equal(1, Count(ics, $"SUMMARY:Night: {secondName} · Screen night"));
        Assert.Contains($"SUMMARY:Night: {firstName} · Screen night", ics);
    }

    [Fact]
    public async Task A_danish_guardian_gets_danish_titles()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Patch.Json(new { Language = "da" }).ToUrl("/users/me/language");
            _.StatusCodeShouldBeOk();
        });
        var monday = FutureMonday();
        await SetOverridesAsync(fixture, guardianToken, child.Id,
            Override(monday, NoteOnly("Skærmaften")),
            Override(monday.AddDays(1), Sleepover("Mormor")));

        var ics = await ReadFeedAsync(fixture, (await CreateIcalTokenAsync(fixture, guardianToken, child.Id)).SubscriptionPath);

        Assert.Contains("X-WR-CALNAME:Nattevagt: Alex", ics);
        Assert.Contains("SUMMARY:Nat: Skærmaften", ics);
        Assert.Contains("SUMMARY:Overnatning: Mormor", ics);
    }

    [Fact]
    public async Task Nights_outside_the_window_are_left_out()
    {
        var (_, guardianToken, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        // A day's margin on each side, since the window follows the child's date, not the UTC one.
        // One request per edge: the dates of one request must lie within 31 days of each other.
        await SetOverridesAsync(fixture, guardianToken, child.Id,
            Override(Today.AddDays(-16), Guardian(guardianId)),
            Override(Today.AddDays(-13), Guardian(guardianId)));
        await SetOverridesAsync(fixture, guardianToken, child.Id,
            Override(Today.AddDays(59), Guardian(guardianId)),
            Override(Today.AddDays(62), Guardian(guardianId)));

        var ics = await ReadFeedAsync(fixture, (await CreateIcalTokenAsync(fixture, guardianToken, child.Id)).SubscriptionPath);

        Assert.Equal(2, Count(ics, "BEGIN:VEVENT"));
        Assert.Contains($"-{Today.AddDays(-13):yyyyMMdd}@buddy-night", ics);
        Assert.Contains($"-{Today.AddDays(59):yyyyMMdd}@buddy-night", ics);
    }

    [Fact]
    public async Task The_feed_is_not_found_once_the_guardian_is_unlinked_from_the_child()
    {
        var coGuardians = await WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);
        var issued = await CreateIcalTokenAsync(fixture, coGuardians.SecondToken, coGuardians.Child.Id);
        await ReadFeedAsync(fixture, issued.SubscriptionPath);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {coGuardians.SecondToken}");
            _.Delete.Url($"/users/me/children/{coGuardians.Child.Id}/guardian-link");
            _.StatusCodeShouldBe(204);
        });

        await FeedShouldBeNotFoundAsync(fixture, issued.SubscriptionPath);
    }

    [Fact]
    public async Task A_wrong_token_or_an_unknown_feed_returns_not_found()
    {
        var (_, guardianToken, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var issued = await CreateIcalTokenAsync(fixture, guardianToken, child.Id);
        var (_, otherToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var otherChild = await GuardianTestHelpers.CreateChildAsync(fixture, otherToken, "Bea");
        var otherIssued = await CreateIcalTokenAsync(fixture, otherToken, otherChild.Id);

        await FeedShouldBeNotFoundAsync(fixture, $"/night-duty/feeds/{guardianId}/ical/not-a-real-token");
        await FeedShouldBeNotFoundAsync(fixture, $"/night-duty/feeds/{Guid.CreateVersion7()}/ical/{issued.Token}");
        // Another guardian's valid token doesn't open this guardian's feed.
        await FeedShouldBeNotFoundAsync(fixture, $"/night-duty/feeds/{guardianId}/ical/{otherIssued.Token}");
    }

    [Fact]
    public async Task Erasing_the_guardian_deletes_the_feed()
    {
        var (_, guardianToken, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var issued = await CreateIcalTokenAsync(fixture, guardianToken, child.Id);
        var eraser = fixture.Host.Services.GetServices<IPersonalDataEraser>().OfType<NightDutyPersonalDataEraser>().Single();

        await eraser.EraseGuardianAsync(new ErasureSubject(new UserId(guardianId), null), CancellationToken.None);

        await FeedShouldBeNotFoundAsync(fixture, issued.SubscriptionPath);
        var feeds = fixture.Host.Services.GetRequiredService<INightFeedEventStore>();
        Assert.Empty(await feeds.ReadAsync(NightFeedId.ForGuardian(new UserId(guardianId)), CancellationToken.None));
    }

    private Task<IScenarioResult> SetCallingNameAsync(string token, string callingName) =>
        fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { CallingName = callingName }).ToUrl("/users/me/calling-name");
            _.StatusCodeShouldBeOk();
        });

    private async Task<string> GivenNameAsync(string token)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url("/users/me");
            _.StatusCodeShouldBeOk();
        });

        using var json = JsonDocument.Parse(response.ReadAsText());

        return json.RootElement.GetProperty("name").GetProperty("givenName").GetString()!;
    }

    private static int Count(string text, string value) => text.Split(value).Length - 1;
}
