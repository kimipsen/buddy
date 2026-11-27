using Alba;

using buddy.Features.FlashCards;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;

namespace buddy.IntegrationTests.Features.FlashCards;

internal static class FlashCardTestHelpers
{
    public static readonly object[] TwoTiers =
    [
        new { CardCount = 5, Stars = 1 },
        new { CardCount = 10, Stars = 3 }
    ];

    public static Task<IScenarioResult> GetAsync(BuddyApiFixture fixture, string token, string url, int status = 200) =>
        fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url(url);
            _.StatusCodeShouldBe(status);
        });

    public static Task<IScenarioResult> PostAsync(BuddyApiFixture fixture, string token, string url, object body, int status = 200) =>
        fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(body).ToUrl(url);
            _.StatusCodeShouldBe(status);
        });

    public static Task<IScenarioResult> PutAsync(BuddyApiFixture fixture, string token, string url, object body, int status = 200) =>
        fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Json(body).ToUrl(url);
            _.StatusCodeShouldBe(status);
        });

    public static Task<IScenarioResult> DeleteAsync(BuddyApiFixture fixture, string token, string url, int status = 204) =>
        fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url(url);
            _.StatusCodeShouldBe(status);
        });

    public static async Task<FlashCardSetResponse> CreateTextSetAsync(BuddyApiFixture fixture, string token, string name = "Capitals") =>
        (await PostAsync(fixture, token, "/flash-cards/sets", new { Name = name, Kind = "Text" })).ReadAsJson<FlashCardSetResponse>();

    public static async Task<FlashCardSetResponse> CreateMathSetAsync(BuddyApiFixture fixture, string token, string name = "Add to 10") =>
        (await PostAsync(fixture, token, "/flash-cards/sets", new { Name = name, Kind = "Math", Math = new { Operation = "Addition", Limit = 10 } }))
            .ReadAsJson<FlashCardSetResponse>();

    public static async Task<FlashCardSetResponse> AddCardAsync(BuddyApiFixture fixture, string token, Guid setId, string question = "Capital of Denmark?", string answer = "Copenhagen") =>
        (await PostAsync(fixture, token, $"/flash-cards/sets/{setId}/cards", new { Question = question, Answer = answer })).ReadAsJson<FlashCardSetResponse>();

    // A Text set with one card, so it can be enabled.
    public static async Task<FlashCardSetResponse> CreatePlayableSetAsync(BuddyApiFixture fixture, string token, string name = "Capitals")
    {
        var set = await CreateTextSetAsync(fixture, token, name);

        return await AddCardAsync(fixture, token, set.Id);
    }

    public static async Task<EnabledFlashCardSetResponse> EnableAsync(BuddyApiFixture fixture, string token, Guid childId, Guid setId, object? tiers = null) =>
        (await PutAsync(fixture, token, $"/flash-cards/children/{childId}/sets/{setId}", new { Tiers = tiers ?? TwoTiers }))
            .ReadAsJson<EnabledFlashCardSetResponse>();

    public static async Task<ChildFlashCardsResponse> GetChildAsync(BuddyApiFixture fixture, string token, Guid childId) =>
        (await GetAsync(fixture, token, $"/flash-cards/children/{childId}")).ReadAsJson<ChildFlashCardsResponse>();

    public static async Task<FlashCardDeckResponse> GetDeckAsync(BuddyApiFixture fixture, string token, Guid setId, Guid tierId) =>
        (await GetAsync(fixture, token, $"/flash-cards/me/sets/{setId}/deck?tierId={tierId}")).ReadAsJson<FlashCardDeckResponse>();

    // The guardian's own sets: the shared catalogue's built-in sets are listed after them.
    public static List<FlashCardSetResponse> OwnSets(IEnumerable<FlashCardSetResponse> sets) => [.. sets.Where(s => s.BuiltInKey is null)];

    // One result per deck slot; the first knownCount slots are "I knew it" (all of them by default).
    public static object[] ResultsFor(FlashCardDeckResponse deck, int? knownCount = null) =>
        [.. deck.Cards.Select((card, slot) => new { Slot = slot, CardKey = card.Key, KnewFirstTime = slot < (knownCount ?? deck.Cards.Count) })];

    public static Task<IScenarioResult> FinishAsync(
        BuddyApiFixture fixture, string token, Guid gameId, Guid setId, Guid tierId, object results, int status = 200, string? idempotencyKey = null) =>
        fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");

            if (idempotencyKey is not null)
            {
                _.WithRequestHeader("Idempotency-Key", idempotencyKey);
            }

            _.Post.Json(new { GameId = gameId, SetId = setId, TierId = tierId, Results = results }).ToUrl("/flash-cards/me/games");
            _.StatusCodeShouldBe(status);
        });

    // Loads the deck for the tier and finishes it with every card known.
    public static async Task<FinishFlashCardGameResponse> PlayAsync(BuddyApiFixture fixture, string childToken, Guid setId, FlashCardTierResponse tier, Guid? gameId = null)
    {
        var deck = await GetDeckAsync(fixture, childToken, setId, tier.Id);

        return (await FinishAsync(fixture, childToken, gameId ?? Guid.NewGuid(), setId, tier.Id, ResultsFor(deck))).ReadAsJson<FinishFlashCardGameResponse>();
    }

    public static async Task<int> TotalStarsAsync(BuddyApiFixture fixture, string childToken) =>
        (await GetAsync(fixture, childToken, "/progress/me")).ReadAsJson<buddy.Features.Progress.ProgressSummary>().TotalStars;

    // A guardian with one child, and that child's own token.
    public static async Task<(string GuardianToken, Guid GuardianId, ChildResponseDto Child, string ChildToken)> CreateFamilyAsync(BuddyApiFixture fixture)
    {
        var (_, guardianToken, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        return (guardianToken, guardianId, child, childToken);
    }
}
