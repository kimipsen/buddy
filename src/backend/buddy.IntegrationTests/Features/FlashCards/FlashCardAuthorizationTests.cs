using buddy.Features.FlashCards;
using buddy.IntegrationTests.Features.WorkLocations;
using buddy.IntegrationTests.Fixtures;

using Xunit;

using static buddy.IntegrationTests.Features.FlashCards.FlashCardTestHelpers;

namespace buddy.IntegrationTests.Features.FlashCards;

// The authorization matrix across the library and the per-child routes.
[Collection(BuddyApiCollection.Name)]
public sealed class FlashCardAuthorizationTests(BuddyApiFixture fixture)
{
    [Fact]
    public async Task A_guardian_sees_their_own_and_a_co_guardians_sets_but_not_an_unrelated_guardians()
    {
        var family = await WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var mine = await CreateTextSetAsync(fixture, family.FirstToken, "Mine");
        var theirs = await CreateTextSetAsync(fixture, family.SecondToken, "Theirs");
        var strangers = await CreateTextSetAsync(fixture, strangerToken, "Stranger's");

        var firstSees = (await GetAsync(fixture, family.FirstToken, "/flash-cards/sets")).ReadAsJson<List<FlashCardSetResponse>>().Select(s => s.Id).ToList();
        var strangerSees = OwnSets((await GetAsync(fixture, strangerToken, "/flash-cards/sets")).ReadAsJson<List<FlashCardSetResponse>>()).Select(s => s.Id).ToList();

        Assert.Contains(mine.Id, firstSees);
        Assert.Contains(theirs.Id, firstSees);
        Assert.DoesNotContain(strangers.Id, firstSees);
        Assert.Equal([strangers.Id], strangerSees);
    }

    [Fact]
    public async Task A_co_guardian_can_edit_and_delete_the_others_set_but_an_unrelated_guardian_gets_not_found()
    {
        var family = await WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var set = await CreateTextSetAsync(fixture, family.FirstToken);

        await PutAsync(fixture, strangerToken, $"/flash-cards/sets/{set.Id}", new { Name = "Hijacked" }, 404);
        await PostAsync(fixture, strangerToken, $"/flash-cards/sets/{set.Id}/cards", new { Question = "q", Answer = "a" }, 404);
        await DeleteAsync(fixture, strangerToken, $"/flash-cards/sets/{set.Id}", 404);

        var renamed = (await PutAsync(fixture, family.SecondToken, $"/flash-cards/sets/{set.Id}", new { Name = "Renamed" })).ReadAsJson<FlashCardSetResponse>();
        Assert.Equal("Renamed", renamed.Name);
        Assert.Equal(family.FirstId, renamed.OwnerId);

        await DeleteAsync(fixture, family.SecondToken, $"/flash-cards/sets/{set.Id}");
    }

    [Fact]
    public async Task A_guardian_who_left_the_family_loses_access_to_the_co_guardians_sets()
    {
        var family = await WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);
        var set = await CreateTextSetAsync(fixture, family.FirstToken);

        await DeleteAsync(fixture, family.SecondToken, $"/users/me/children/{family.Child.Id}/guardian-link");

        var listed = (await GetAsync(fixture, family.SecondToken, "/flash-cards/sets")).ReadAsJson<List<FlashCardSetResponse>>();
        Assert.DoesNotContain(listed, s => s.Id == set.Id);
        await PutAsync(fixture, family.SecondToken, $"/flash-cards/sets/{set.Id}", new { Name = "Still mine?" }, 404);
        await GetAsync(fixture, family.SecondToken, $"/flash-cards/children/{family.Child.Id}", 404);
        await PutAsync(fixture, family.SecondToken, $"/flash-cards/children/{family.Child.Id}/sets/{set.Id}", new { Tiers = TwoTiers }, 404);
    }

    [Fact]
    public async Task A_child_cannot_manage_the_library_or_a_childs_practice()
    {
        var (guardianToken, _, child, childToken) = await CreateFamilyAsync(fixture);
        var set = await CreatePlayableSetAsync(fixture, guardianToken);
        await EnableAsync(fixture, guardianToken, child.Id, set.Id);

        await GetAsync(fixture, childToken, "/flash-cards/sets", 403);
        await PostAsync(fixture, childToken, "/flash-cards/sets", new { Name = "Mine", Kind = "Text" }, 403);
        await PutAsync(fixture, childToken, $"/flash-cards/sets/{set.Id}", new { Name = "Mine" }, 403);
        await DeleteAsync(fixture, childToken, $"/flash-cards/sets/{set.Id}", 403);
        await PostAsync(fixture, childToken, $"/flash-cards/sets/{set.Id}/cards", new { Question = "q", Answer = "a" }, 403);
        await PutAsync(fixture, childToken, $"/flash-cards/sets/{set.Id}/cards/{set.Cards[0].Id}", new { Question = "q", Answer = "a" }, 403);
        await DeleteAsync(fixture, childToken, $"/flash-cards/sets/{set.Id}/cards/{set.Cards[0].Id}", 403);
        await PutAsync(fixture, childToken, $"/flash-cards/children/{child.Id}/sets/{set.Id}", new { Tiers = TwoTiers }, 403);
        await DeleteAsync(fixture, childToken, $"/flash-cards/children/{child.Id}/sets/{set.Id}", 403);

        // Reading their own enabled sets is allowed.
        Assert.Single((await GetChildAsync(fixture, childToken, child.Id)).EnabledSets);
    }

    [Fact]
    public async Task A_guardian_can_only_configure_children_they_are_linked_to()
    {
        var (_, _, child, _) = await CreateFamilyAsync(fixture);
        var (_, otherToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var set = await CreatePlayableSetAsync(fixture, otherToken);

        await GetAsync(fixture, otherToken, $"/flash-cards/children/{child.Id}", 404);
        await PutAsync(fixture, otherToken, $"/flash-cards/children/{child.Id}/sets/{set.Id}", new { Tiers = TwoTiers }, 404);
        await DeleteAsync(fixture, otherToken, $"/flash-cards/children/{child.Id}/sets/{set.Id}", 404);
    }

    [Fact]
    public async Task A_child_cannot_read_another_childs_flash_cards()
    {
        var (_, _, firstChild, _) = await CreateFamilyAsync(fixture);
        var (_, _, _, otherChildToken) = await CreateFamilyAsync(fixture);

        await GetAsync(fixture, otherChildToken, $"/flash-cards/children/{firstChild.Id}", 404);
    }
}
