using Alba;

using buddy.Features.FlashCards;
using buddy.Features.Users;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

using Xunit;

using static buddy.IntegrationTests.Features.FlashCards.FlashCardTestHelpers;

namespace buddy.IntegrationTests.Features.FlashCards.FinishFlashCardGame;

// The once-per-set-per-day cap is keyed by the child's LOCAL date on the finish day, from the
// injected TimeProvider. These run on one extra shared host with a fake clock (users, sets and
// tokens are created through the normal host: it is the same database).
[Collection(BuddyApiCollection.Name)]
public sealed class FinishFlashCardGameDayBoundaryTests(BuddyApiFixture fixture)
{
    private static readonly FakeTimeProvider Clock = new();

    private Task<IAlbaHost> ClockHost() => fixture.SharedHostAsync(
        "flashcards-clock",
        new Dictionary<string, string?>(),
        services => services.Replace(ServiceDescriptor.Singleton<TimeProvider>(Clock)));

    private static async Task<IScenarioResult> SendAsync(IAlbaHost host, string token, Action<Scenario> configure) =>
        await host.Scenario(s =>
        {
            s.WithRequestHeader("Authorization", $"Bearer {token}");
            configure(s);
        });

    private static async Task<FinishFlashCardGameResponse> FinishOnClockAsync(IAlbaHost host, string token, FlashCardSetResponse set, FlashCardTierResponse tier, FlashCardDeckResponse deck) =>
        (await SendAsync(host, token, s =>
        {
            s.Post.Json(new { GameId = Guid.NewGuid(), SetId = set.Id, TierId = tier.Id, Results = ResultsFor(deck) }).ToUrl("/flash-cards/me/games");
            s.StatusCodeShouldBe(200);
        })).ReadAsJson<FinishFlashCardGameResponse>();

    private static async Task<FlashCardDeckResponse> DeckOnClockAsync(IAlbaHost host, string token, FlashCardSetResponse set, FlashCardTierResponse tier) =>
        (await SendAsync(host, token, s =>
        {
            s.Get.Url($"/flash-cards/me/sets/{set.Id}/deck?tierId={tier.Id}");
            s.StatusCodeShouldBe(200);
        })).ReadAsJson<FlashCardDeckResponse>();

    // The UTC instant of a local wall-clock time in the child's zone.
    private async Task<DateTimeOffset> LocalAsync(Guid childId, DateTime wallClock)
    {
        var users = fixture.Host.Services.GetRequiredService<IUserEventStore>();
        var user = (await users.FindSnapshotAsync(new UserId(childId), CancellationToken.None))!;
        var zone = TimeZoneInfo.FindSystemTimeZoneById(user.TimeZoneId.Value);

        return new DateTimeOffset(wallClock, zone.GetUtcOffset(wallClock)).ToUniversalTime();
    }

    [Fact]
    public async Task The_cap_resets_at_the_childs_local_midnight_and_a_game_started_before_it_counts_on_the_finish_day()
    {
        var host = await ClockHost();
        var (guardianToken, _, child, childToken) = await CreateFamilyAsync(fixture);
        var set = await CreatePlayableSetAsync(fixture, guardianToken);
        var enabled = await EnableAsync(fixture, guardianToken, child.Id, set.Id);
        var tier = enabled.Tiers[0];
        var day = new DateTime(2031, 3, 14);

        Clock.SetUtcNow(await LocalAsync(child.Id, day.AddHours(23).AddMinutes(58)));
        var lateDeck = await DeckOnClockAsync(host, childToken, set, tier);
        var late = await FinishOnClockAsync(host, childToken, set, tier, lateDeck);
        Clock.SetUtcNow(await LocalAsync(child.Id, day.AddHours(23).AddMinutes(59)));
        var sameDay = await FinishOnClockAsync(host, childToken, set, tier, lateDeck);

        // Started before midnight, finished after: the finish day counts.
        var startedBeforeMidnight = await DeckOnClockAsync(host, childToken, set, tier);
        Clock.SetUtcNow(await LocalAsync(child.Id, day.AddDays(1).AddMinutes(1)));
        var nextDay = await FinishOnClockAsync(host, childToken, set, tier, startedBeforeMidnight);

        Assert.Equal(1, late.Stars);
        Assert.Equal(DateOnly.FromDateTime(day), late.Game.FinishedOn);
        Assert.Equal(0, sameDay.Stars);
        Assert.Equal(1, nextDay.Stars);
        Assert.Equal(DateOnly.FromDateTime(day.AddDays(1)), nextDay.Game.FinishedOn);
        Assert.Equal(2, nextDay.TotalStars);
    }
}
