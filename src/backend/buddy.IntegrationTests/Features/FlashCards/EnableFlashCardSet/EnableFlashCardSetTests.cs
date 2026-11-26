using buddy.Features.FlashCards;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.FlashCards.FlashCardTestHelpers;

namespace buddy.IntegrationTests.Features.FlashCards.EnableFlashCardSet;

[Collection(BuddyApiCollection.Name)]
public sealed class EnableFlashCardSetTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("EnableFlashCardSet")]
    public async Task A_guardian_can_enable_a_set_for_a_child_with_tiers()
    {
        var (guardianToken, _, child, _) = await CreateFamilyAsync(fixture);
        var set = await CreatePlayableSetAsync(fixture, guardianToken, "Capitals");

        var enabled = await EnableAsync(fixture, guardianToken, child.Id, set.Id);

        Assert.Equal(set.Id, enabled.SetId);
        Assert.Equal("Capitals", enabled.SetName);
        Assert.Equal([(5, 1), (10, 3)], enabled.Tiers.Select(t => (t.CardCount, t.Stars)));
        Assert.All(enabled.Tiers, t => Assert.NotEqual(Guid.Empty, t.Id));
        Assert.Equal(2, enabled.Tiers.Select(t => t.Id).Distinct().Count());
    }

    [Fact]
    public async Task A_math_set_can_be_enabled()
    {
        var (guardianToken, _, child, _) = await CreateFamilyAsync(fixture);
        var set = await CreateMathSetAsync(fixture, guardianToken);

        await EnableAsync(fixture, guardianToken, child.Id, set.Id);

        Assert.Single((await GetChildAsync(fixture, guardianToken, child.Id)).EnabledSets);
    }

    [Fact]
    public async Task Replacing_the_tiers_keeps_the_ids_of_the_tiers_that_are_named()
    {
        var (guardianToken, _, child, _) = await CreateFamilyAsync(fixture);
        var set = await CreatePlayableSetAsync(fixture, guardianToken);
        var first = await EnableAsync(fixture, guardianToken, child.Id, set.Id);
        var kept = first.Tiers[1];

        var second = await EnableAsync(
            fixture,
            guardianToken,
            child.Id,
            set.Id,
            new object[] { new { kept.Id, CardCount = 12, Stars = 4 }, new { CardCount = 20, Stars = 8 } });

        Assert.Equal(kept.Id, second.Tiers[0].Id);
        Assert.Equal((12, 4), (second.Tiers[0].CardCount, second.Tiers[0].Stars));
        Assert.NotEqual(first.Tiers[0].Id, second.Tiers[1].Id);

        var stored = Assert.Single((await GetChildAsync(fixture, guardianToken, child.Id)).EnabledSets);
        Assert.Equal(second.Tiers.Select(t => t.Id), stored.Tiers.Select(t => t.Id));
    }

    [Fact]
    public async Task Enabling_a_second_set_keeps_the_first()
    {
        var (guardianToken, _, child, _) = await CreateFamilyAsync(fixture);
        var first = await CreatePlayableSetAsync(fixture, guardianToken, "One");
        var second = await CreatePlayableSetAsync(fixture, guardianToken, "Two");

        await EnableAsync(fixture, guardianToken, child.Id, first.Id);
        await EnableAsync(fixture, guardianToken, child.Id, second.Id);

        Assert.Equal([first.Id, second.Id], (await GetChildAsync(fixture, guardianToken, child.Id)).EnabledSets.Select(s => s.SetId));
    }

    [Fact]
    public async Task Repeating_the_same_tiers_records_nothing_new()
    {
        var (guardianToken, _, child, _) = await CreateFamilyAsync(fixture);
        var set = await CreatePlayableSetAsync(fixture, guardianToken);
        var practices = fixture.Host.Services.GetRequiredService<IFlashCardPracticeEventStore>();

        var first = await EnableAsync(fixture, guardianToken, child.Id, set.Id);
        var again = await EnableAsync(
            fixture,
            guardianToken,
            child.Id,
            set.Id,
            first.Tiers.Select(t => new { t.Id, t.CardCount, t.Stars }).ToArray());

        Assert.Equal(first.Tiers.Select(t => t.Id), again.Tiers.Select(t => t.Id));
        // Started and one Enabled.
        Assert.Equal(2, (await practices.ReadAsync(FlashCardPracticeId.ForChild(new(child.Id)), CancellationToken.None)).Count);
    }

    [Fact]
    public async Task A_co_guardians_set_can_be_enabled()
    {
        var family = await Features.WorkLocations.WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);
        var set = await CreatePlayableSetAsync(fixture, family.FirstToken);

        var enabled = await EnableAsync(fixture, family.SecondToken, family.Child.Id, set.Id);

        Assert.Equal(set.Id, enabled.SetId);
    }

    public static TheoryData<string, object[]> InvalidTiers => new()
    {
        { "no tiers", [] },
        { "six tiers", Enumerable.Range(0, 6).Select(i => (object)new { CardCount = 3 + i, Stars = 1 }).ToArray() },
        { "too few cards", [new { CardCount = 2, Stars = 1 }] },
        { "too many cards", [new { CardCount = 51, Stars = 1 }] },
        { "no stars", [new { CardCount = 5, Stars = 0 }] },
        { "too many stars", [new { CardCount = 5, Stars = 21 }] },
        { "duplicate counts", [new { CardCount = 5, Stars = 1 }, new { CardCount = 5, Stars = 2 }] },
        { "duplicate ids", [new { Id = Guid.Empty, CardCount = 5, Stars = 1 }, new { Id = Guid.Empty, CardCount = 6, Stars = 2 }] },
        { "an unknown id", [new { Id = Guid.CreateVersion7(), CardCount = 5, Stars = 1 }] },
    };

    [Theory]
    [MemberData(nameof(InvalidTiers))]
    public async Task Invalid_tiers_are_rejected(string reason, object[] tiers)
    {
        Assert.NotEmpty(reason);
        var (guardianToken, _, child, _) = await CreateFamilyAsync(fixture);
        var set = await CreatePlayableSetAsync(fixture, guardianToken);

        await PutAsync(fixture, guardianToken, $"/flash-cards/children/{child.Id}/sets/{set.Id}", new { Tiers = tiers }, 400);

        Assert.Empty((await GetChildAsync(fixture, guardianToken, child.Id)).EnabledSets);
    }

    [Theory]
    [InlineData(3, 1)]
    [InlineData(50, 20)]
    public async Task The_tier_limits_themselves_are_accepted(int cardCount, int stars)
    {
        var (guardianToken, _, child, _) = await CreateFamilyAsync(fixture);
        var set = await CreatePlayableSetAsync(fixture, guardianToken);

        await EnableAsync(fixture, guardianToken, child.Id, set.Id, new[] { new { CardCount = cardCount, Stars = stars } });
    }

    [Fact]
    public async Task Five_tiers_are_accepted()
    {
        var (guardianToken, _, child, _) = await CreateFamilyAsync(fixture);
        var set = await CreatePlayableSetAsync(fixture, guardianToken);

        var enabled = await EnableAsync(fixture, guardianToken, child.Id, set.Id, Enumerable.Range(0, 5).Select(i => new { CardCount = 3 + i, Stars = 1 + i }).ToArray());

        Assert.Equal(5, enabled.Tiers.Count);
    }

    [Fact]
    public async Task A_set_without_a_card_cannot_be_enabled()
    {
        var (guardianToken, _, child, _) = await CreateFamilyAsync(fixture);
        var empty = await CreateTextSetAsync(fixture, guardianToken);

        await PutAsync(fixture, guardianToken, $"/flash-cards/children/{child.Id}/sets/{empty.Id}", new { Tiers = TwoTiers }, 400);
    }

    [Fact]
    public async Task An_unknown_unreachable_or_deleted_set_is_not_found()
    {
        var (guardianToken, _, child, _) = await CreateFamilyAsync(fixture);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var strangers = await CreatePlayableSetAsync(fixture, strangerToken);
        var deleted = await CreatePlayableSetAsync(fixture, guardianToken);
        await DeleteAsync(fixture, guardianToken, $"/flash-cards/sets/{deleted.Id}");

        await PutAsync(fixture, guardianToken, $"/flash-cards/children/{child.Id}/sets/{Guid.CreateVersion7()}", new { Tiers = TwoTiers }, 404);
        await PutAsync(fixture, guardianToken, $"/flash-cards/children/{child.Id}/sets/{strangers.Id}", new { Tiers = TwoTiers }, 404);
        await PutAsync(fixture, guardianToken, $"/flash-cards/children/{child.Id}/sets/{deleted.Id}", new { Tiers = TwoTiers }, 404);
    }
}
