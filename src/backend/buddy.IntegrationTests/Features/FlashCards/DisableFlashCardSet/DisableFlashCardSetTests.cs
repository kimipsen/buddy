using buddy.Features.FlashCards;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.FlashCards.FlashCardTestHelpers;

namespace buddy.IntegrationTests.Features.FlashCards.DisableFlashCardSet;

[Collection(BuddyApiCollection.Name)]
public sealed class DisableFlashCardSetTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("DisableFlashCardSet")]
    public async Task A_guardian_can_disable_a_set_for_a_child()
    {
        var (guardianToken, _, child, _) = await CreateFamilyAsync(fixture);
        var kept = await CreatePlayableSetAsync(fixture, guardianToken, "Kept");
        var removed = await CreatePlayableSetAsync(fixture, guardianToken, "Removed");
        await EnableAsync(fixture, guardianToken, child.Id, kept.Id);
        await EnableAsync(fixture, guardianToken, child.Id, removed.Id);

        await DeleteAsync(fixture, guardianToken, $"/flash-cards/children/{child.Id}/sets/{removed.Id}");

        Assert.Equal([kept.Id], (await GetChildAsync(fixture, guardianToken, child.Id)).EnabledSets.Select(s => s.SetId));
    }

    [Fact]
    public async Task Disabling_a_set_that_is_not_enabled_succeeds_and_records_nothing()
    {
        var (guardianToken, _, child, _) = await CreateFamilyAsync(fixture);
        var set = await CreatePlayableSetAsync(fixture, guardianToken);
        var practices = fixture.Host.Services.GetRequiredService<IFlashCardPracticeEventStore>();
        var practiceId = FlashCardPracticeId.ForChild(new(child.Id));

        // No practice stream at all.
        await DeleteAsync(fixture, guardianToken, $"/flash-cards/children/{child.Id}/sets/{set.Id}");
        Assert.Empty(await practices.ReadAsync(practiceId, CancellationToken.None));

        // A stream, but not this set; and twice for the same set.
        var other = await CreatePlayableSetAsync(fixture, guardianToken, "Other");
        await EnableAsync(fixture, guardianToken, child.Id, other.Id);
        await DeleteAsync(fixture, guardianToken, $"/flash-cards/children/{child.Id}/sets/{set.Id}");
        await DeleteAsync(fixture, guardianToken, $"/flash-cards/children/{child.Id}/sets/{other.Id}");
        await DeleteAsync(fixture, guardianToken, $"/flash-cards/children/{child.Id}/sets/{other.Id}");

        // Started, Enabled, Disabled.
        Assert.Equal(3, (await practices.ReadAsync(practiceId, CancellationToken.None)).Count);
    }

    [Fact]
    public async Task A_deleted_set_can_still_be_taken_off_a_child()
    {
        var (guardianToken, _, child, _) = await CreateFamilyAsync(fixture);
        var set = await CreatePlayableSetAsync(fixture, guardianToken);
        await EnableAsync(fixture, guardianToken, child.Id, set.Id);
        await DeleteAsync(fixture, guardianToken, $"/flash-cards/sets/{set.Id}");

        await DeleteAsync(fixture, guardianToken, $"/flash-cards/children/{child.Id}/sets/{set.Id}");

        Assert.Empty((await GetChildAsync(fixture, guardianToken, child.Id)).EnabledSets);
    }
}
