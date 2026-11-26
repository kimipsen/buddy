using buddy.Features.FlashCards;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.FlashCards.FlashCardTestHelpers;

namespace buddy.IntegrationTests.Features.FlashCards.GetChildFlashCards;

[Collection(BuddyApiCollection.Name)]
public sealed class GetChildFlashCardsTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("GetChildFlashCards")]
    public async Task A_guardian_sees_what_is_enabled_for_their_child()
    {
        var (guardianToken, _, child, _) = await CreateFamilyAsync(fixture);
        var set = await CreatePlayableSetAsync(fixture, guardianToken, "Capitals");
        await EnableAsync(fixture, guardianToken, child.Id, set.Id);

        var practice = await GetChildAsync(fixture, guardianToken, child.Id);

        Assert.Equal(child.Id, practice.ChildId);
        var enabled = Assert.Single(practice.EnabledSets);
        Assert.Equal(set.Id, enabled.SetId);
        Assert.Equal("Capitals", enabled.SetName);
        Assert.Equal([(5, 1), (10, 3)], enabled.Tiers.Select(t => (t.CardCount, t.Stars)));
        Assert.Empty(practice.Games);
    }

    [Fact]
    public async Task A_child_with_nothing_enabled_gets_an_empty_answer_not_not_found()
    {
        var (guardianToken, _, child, childToken) = await CreateFamilyAsync(fixture);

        Assert.Empty((await GetChildAsync(fixture, guardianToken, child.Id)).EnabledSets);
        Assert.Empty((await GetChildAsync(fixture, childToken, child.Id)).EnabledSets);
    }

    [Fact]
    public async Task A_child_sees_their_own_enabled_sets()
    {
        var (guardianToken, _, child, childToken) = await CreateFamilyAsync(fixture);
        var set = await CreatePlayableSetAsync(fixture, guardianToken);
        await EnableAsync(fixture, guardianToken, child.Id, set.Id);

        var enabled = Assert.Single((await GetChildAsync(fixture, childToken, child.Id)).EnabledSets);

        Assert.Equal(set.Id, enabled.SetId);
    }
}
