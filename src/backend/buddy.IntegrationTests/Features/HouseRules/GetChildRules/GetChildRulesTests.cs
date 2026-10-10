using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.WorkLocations;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

using static buddy.IntegrationTests.Features.HouseRules.HouseRulesTestHelpers;

namespace buddy.IntegrationTests.Features.HouseRules.GetChildRules;

[Collection(BuddyApiCollection.Name)]
public sealed class GetChildRulesTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("GetChildRules")]
    public async Task A_child_in_two_homes_sees_their_personal_rules_and_two_labelled_households()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Emil");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var mum = await GroupTestHelpers.CreateGroupAsync(fixture, token, "Mum's");
        var dad = await GroupTestHelpers.CreateGroupAsync(fixture, token, "Dad's");
        await AddChildToGroupAsync(fixture, token, mum, child.Id);
        await AddChildToGroupAsync(fixture, token, dad, child.Id);

        var personal = await AddOneRuleAsync(fixture, token, ChildRules(child.Id), "Gaming");
        await AddOneRuleAsync(fixture, token, GroupRules(mum), "Shoes off");
        await AddOneRuleAsync(fixture, token, GroupRules(dad), "Dinner");
        await AcknowledgeAsync(fixture, childToken, ChildRules(child.Id), personal.Id, 1);

        var rules = await GetChildRulesAsync(fixture, childToken, child.Id);

        Assert.Equal(child.Id, rules!.ChildId);
        Assert.Equal("Child", rules.Personal.ScopeKind);
        Assert.Equal("Emil", rules.Personal.Label);
        var gaming = Assert.Single(rules.Personal.Rules);
        Assert.True(gaming.IsUpToDate);
        Assert.Equal(1, gaming.AcknowledgedRevision);

        Assert.Equal(["Dad's", "Mum's"], rules.Households.Select(h => h.Label));
        Assert.Equal([dad, mum], rules.Households.Select(h => h.ScopeId));
        Assert.All(rules.Households, h => Assert.Equal("Group", h.ScopeKind));
        Assert.Null(Assert.Single(rules.Households[0].Rules).AcknowledgedRevision);
        Assert.Equal(2, rules.PendingAcknowledgements);

        var asGuardian = await GetChildRulesAsync(fixture, token, child.Id);
        Assert.Equivalent(rules, asGuardian, strict: true);
    }

    [Fact]
    public async Task A_guardian_sees_the_household_of_the_other_home_even_without_being_a_member()
    {
        var family = await WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);
        var otherHome = await GroupTestHelpers.CreateGroupAsync(fixture, family.SecondToken, "Other home");
        await AddChildToGroupAsync(fixture, family.SecondToken, otherHome, family.Child.Id);
        await AddOneRuleAsync(fixture, family.SecondToken, GroupRules(otherHome), "Lights out at 20:00");

        var rules = await GetChildRulesAsync(fixture, family.FirstToken, family.Child.Id);

        var household = Assert.Single(rules!.Households);
        Assert.Equal("Other home", household.Label);
        Assert.Equal("Lights out at 20:00", Assert.Single(household.Rules).Title);
        // Read-only: the first guardian isn't a member, so the group book itself is out of reach.
        await ListRulesAsync(fixture, family.FirstToken, GroupRules(otherHome), expectedStatus: 404);
    }

    [Fact]
    public async Task Leaving_or_deleting_a_household_drops_its_section_immediately()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var left = await GroupTestHelpers.CreateGroupAsync(fixture, token, "Left");
        var deleted = await GroupTestHelpers.CreateGroupAsync(fixture, token, "Deleted");
        await AddChildToGroupAsync(fixture, token, left, child.Id);
        await AddChildToGroupAsync(fixture, token, deleted, child.Id);
        await AddOneRuleAsync(fixture, token, GroupRules(left));
        await AddOneRuleAsync(fixture, token, GroupRules(deleted));
        Assert.Equal(2, (await GetChildRulesAsync(fixture, token, child.Id))!.Households.Count);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url($"/groups/{left}/members/{child.Id}");
            _.StatusCodeShouldBe(204);
        });
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url($"/groups/{deleted}");
            _.StatusCodeShouldBe(204);
        });

        var rules = await GetChildRulesAsync(fixture, token, child.Id);
        Assert.Empty(rules!.Households);
        Assert.Equal(0, rules.PendingAcknowledgements);
    }

    [Fact]
    public async Task Strangers_and_adult_household_members_get_not_found()
    {
        var home = await CreateHouseholdAsync(fixture);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await GetChildRulesAsync(fixture, strangerToken, home.Child.Id, expectedStatus: 404);
        // Sharing a household doesn't make the adult the child's guardian.
        await GetChildRulesAsync(fixture, home.AdultToken, home.Child.Id, expectedStatus: 404);
    }
}
