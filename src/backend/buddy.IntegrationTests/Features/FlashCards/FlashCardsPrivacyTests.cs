using System.Text.Json;

using Alba;

using buddy.Features.FlashCards;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Privacy;
using buddy.IntegrationTests.Features.WorkLocations;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.FlashCards.FlashCardTestHelpers;

namespace buddy.IntegrationTests.Features.FlashCards;

// Flash cards in the personal data export and in account erasure -- gdpr-data-protection.md,
// Questions 2, 3 and 5. Every personal value is unique to its test.
[Collection(BuddyApiCollection.Name)]
public sealed class FlashCardsPrivacyTests(BuddyApiFixture fixture)
{
    [Fact]
    public async Task A_guardians_export_has_the_childs_enabled_sets_and_every_game_with_its_card_results()
    {
        var n = Unique();
        var (guardianToken, _, child, childToken) = await CreateFamilyAsync(fixture);
        var set = await CreateTextSetAsync(fixture, guardianToken, $"Rivers{n}");
        await AddCardAsync(fixture, guardianToken, set.Id, $"Longest river {n}?", $"Nile {n}");
        var enabled = await EnableAsync(fixture, guardianToken, child.Id, set.Id);
        await PlayAsync(fixture, childToken, set.Id, enabled.Tiers[0]);

        using var json = JsonDocument.Parse(await (await ExportAsync(guardianToken)).ReadAsTextAsync());
        var sections = json.RootElement.GetProperty("sections");

        var practice = Assert.Single(sections.GetProperty("flashCards").EnumerateArray());
        Assert.Equal(child.Id, practice.GetProperty("childId").GetGuid());
        Assert.Equal(["childId", "enabledSets", "games"], practice.EnumerateObject().Select(property => property.Name).Order());
        Assert.False(practice.TryGetProperty("cardWeights", out _));
        Assert.Equal($"Rivers{n}", Assert.Single(practice.GetProperty("enabledSets").EnumerateArray()).GetProperty("setName").GetString());
        var game = Assert.Single(practice.GetProperty("games").EnumerateArray());
        Assert.Equal($"Rivers{n}", game.GetProperty("setName").GetString());
        // The set has one card, which fills every slot of the tier's game.
        var results = game.GetProperty("results").EnumerateArray().ToArray();
        Assert.Equal(game.GetProperty("cardCount").GetInt32(), results.Length);
        var result = results[0];
        Assert.Equal($"Longest river {n}?", result.GetProperty("question").GetString());
        Assert.Equal($"Nile {n}", result.GetProperty("answer").GetString());
        Assert.True(result.GetProperty("knewFirstTime").GetBoolean());
        Assert.False(result.TryGetProperty("cardKey", out _));

        // The stars the game paid out, per set and day, are in the progress section.
        var starsByDay = Assert.Single(sections.GetProperty("progress").EnumerateArray()).GetProperty("flashCardStarsByDay");
        Assert.Equal(set.Id, Assert.Single(starsByDay.EnumerateArray()).GetProperty("setId").GetGuid());
    }

    [Fact]
    public async Task The_export_lists_every_game_not_only_the_window_the_snapshot_keeps()
    {
        var (guardianToken, _, child, _) = await CreateFamilyAsync(fixture);
        var set = await CreatePlayableSetAsync(fixture, guardianToken);
        var enabled = await EnableAsync(fixture, guardianToken, child.Id, set.Id);
        var practices = fixture.Host.Services.GetRequiredService<IFlashCardPracticeEventStore>();
        var id = FlashCardPracticeId.ForChild(new UserId(child.Id));

        FlashCardPracticeEvent[] games =
        [
            .. Enumerable.Range(0, FlashCardPractice.MaxGames + 5).Select(i => (FlashCardPracticeEvent)new FlashCardGameFinished(
                id, Guid.CreateVersion7(), new FlashCardSetId(set.Id), "Capitals", new FlashCardTierId(enabled.Tiers[0].Id), new DateOnly(2025, 1, 1),
                1, [new FlashCardResult(0, "k", "Q", "A", true, 1)], 0, DateTimeOffset.UnixEpoch))
        ];
        await practices.AppendAsync(id, games, CancellationToken.None);

        using var json = JsonDocument.Parse(await (await ExportAsync(guardianToken)).ReadAsTextAsync());
        var practice = Assert.Single(json.RootElement.GetProperty("sections").GetProperty("flashCards").EnumerateArray());

        Assert.Equal(FlashCardPractice.MaxGames + 5, practice.GetProperty("games").GetArrayLength());
    }

    [Fact]
    public async Task Erasing_the_only_guardian_erases_the_childs_practice_and_soft_deletes_their_sets()
    {
        var n = Unique();
        var (guardianToken, _, child, childToken) = await CreateFamilyAsync(fixture);
        var set = await CreateTextSetAsync(fixture, guardianToken, $"Rivers{n}");
        await AddCardAsync(fixture, guardianToken, set.Id, $"Longest river {n}?", $"Nile {n}");
        var enabled = await EnableAsync(fixture, guardianToken, child.Id, set.Id);
        await PlayAsync(fixture, childToken, set.Id, enabled.Tiers[0]);
        var practices = fixture.Host.Services.GetRequiredService<IFlashCardPracticeEventStore>();
        var sets = fixture.Host.Services.GetRequiredService<IFlashCardSetEventStore>();
        var practiceId = FlashCardPracticeId.ForChild(new UserId(child.Id));
        Assert.NotEmpty(await practices.ReadAsync(practiceId, CancellationToken.None));

        await DeleteAccountAsync(guardianToken);

        Assert.Empty(await practices.ReadAsync(practiceId, CancellationToken.None));
        Assert.Null(await practices.FindSnapshotAsync(practiceId, CancellationToken.None));
        Assert.True((await sets.FindSnapshotAsync(new FlashCardSetId(set.Id), CancellationToken.None))!.IsDeleted);

        // The child's results and set history are gone from every table; the set the guardian
        // authored stays as a soft-deleted stream, so its own name and cards are not asserted here.
        // (The user stream itself keeps the pseudonymised id, so only this feature's tables are checked.)
        var leftovers = (await PersonalDataScanner.FindAsync(fixture, child.Id.ToString()))
            .Where(hit => hit.Contains("flashcard", StringComparison.Ordinal))
            .ToArray();
        Assert.True(leftovers.Length == 0, $"Personal data left after erasure:\n{string.Join("\n", leftovers)}");
    }

    [Fact]
    public async Task Erasing_a_co_guardian_soft_deletes_only_their_sets_and_keeps_the_childs_practice()
    {
        var family = await WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);
        var erasedSet = await CreatePlayableSetAsync(fixture, family.FirstToken, "Erased guardian's set");
        var keptSet = await CreatePlayableSetAsync(fixture, family.SecondToken, "Kept guardian's set");
        await EnableAsync(fixture, family.SecondToken, family.Child.Id, keptSet.Id);
        var practices = fixture.Host.Services.GetRequiredService<IFlashCardPracticeEventStore>();
        var sets = fixture.Host.Services.GetRequiredService<IFlashCardSetEventStore>();

        await DeleteAccountAsync(family.FirstToken);

        Assert.True((await sets.FindSnapshotAsync(new FlashCardSetId(erasedSet.Id), CancellationToken.None))!.IsDeleted);
        Assert.False((await sets.FindSnapshotAsync(new FlashCardSetId(keptSet.Id), CancellationToken.None))!.IsDeleted);
        Assert.NotNull(await practices.FindSnapshotAsync(FlashCardPracticeId.ForChild(new UserId(family.Child.Id)), CancellationToken.None));
    }

    [Fact]
    public async Task Erasing_a_guardian_twice_is_harmless()
    {
        var (_, guardianToken, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var set = await CreatePlayableSetAsync(fixture, guardianToken);
        var sets = fixture.Host.Services.GetRequiredService<IFlashCardSetEventStore>();
        var eraser = fixture.Host.Services.GetServices<buddy.Common.Erasure.IPersonalDataEraser>().OfType<FlashCardsPersonalDataEraser>().Single();
        var subject = new buddy.Common.Erasure.ErasureSubject(new UserId(guardianId), null);

        await eraser.EraseGuardianAsync(subject, CancellationToken.None);
        var events = await sets.ReadAsync(new FlashCardSetId(set.Id), CancellationToken.None);
        await eraser.EraseGuardianAsync(subject, CancellationToken.None);

        Assert.Equal(events.Count, (await sets.ReadAsync(new FlashCardSetId(set.Id), CancellationToken.None)).Count);
        Assert.Single(events, e => e.Value is FlashCardSetDeleted);
    }

    private Task<IScenarioResult> ExportAsync(string token) =>
        fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url("/users/me/export");
            _.StatusCodeShouldBe(200);
        });

    private Task<IScenarioResult> DeleteAccountAsync(string token) =>
        fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url("/users/me");
            _.StatusCodeShouldBe(204);
        });

    private static string Unique() => Guid.NewGuid().ToString("N")[..12];
}
