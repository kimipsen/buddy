using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.WorkLocations;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

using static buddy.IntegrationTests.Features.HouseRules.HouseRulesTestHelpers;

namespace buddy.IntegrationTests.Features.HouseRules.ListRules;

[Collection(BuddyApiCollection.Name)]
public sealed class ListRulesTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ListRules")]
    public async Task A_scope_with_no_rules_yet_is_an_empty_list_not_not_found()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);

        var book = await ListRulesAsync(fixture, token, ChildRules(child.Id));

        Assert.Equal("Child", book!.ScopeKind);
        Assert.Equal("Manage", book.Access);
        Assert.Equal([child.Id], book.Children);
        Assert.Empty(book.Rules);
    }

    [Fact]
    public async Task A_child_reads_their_own_book_at_the_acknowledge_tier()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        await AddOneRuleAsync(fixture, token, ChildRules(child.Id));

        var book = await ListRulesAsync(fixture, childToken, ChildRules(child.Id));

        Assert.Equal("Acknowledge", book!.Access);
        Assert.Equal([child.Id], book.Children);
        Assert.Single(book.Rules);
    }

    [Fact]
    [CoversEndpoint("ListRulesForGroup")]
    public async Task Adult_members_see_every_childs_status_and_a_child_member_sees_only_their_own()
    {
        var home = await CreateHouseholdAsync(fixture);
        var sibling = await GuardianTestHelpers.CreateChildAsync(fixture, home.OwnerToken, "Ida");
        await AddChildToGroupAsync(fixture, home.OwnerToken, home.GroupId, sibling.Id);
        await AddOneRuleAsync(fixture, home.OwnerToken, home.Rules, "Dinner");

        var asAdult = await ListRulesAsync(fixture, home.AdultToken, home.Rules);
        Assert.Equal("View", asAdult!.Access);
        Assert.Equal([home.Child.Id, sibling.Id], asAdult.Children.Order());
        Assert.Equal(2, Assert.Single(asAdult.Rules).Acknowledgements.Count);

        var asChild = await ListRulesAsync(fixture, home.ChildToken, home.Rules);
        Assert.Equal("Acknowledge", asChild!.Access);
        Assert.Equal([home.Child.Id], asChild.Children);
        Assert.Equal(home.Child.Id, Assert.Single(Assert.Single(asChild.Rules).Acknowledgements).ChildId);

        Assert.Equal("Manage", (await ListRulesAsync(fixture, home.OwnerToken, home.Rules))!.Access);
    }

    [Fact]
    public async Task Strangers_and_deleted_groups_get_not_found()
    {
        var home = await CreateHouseholdAsync(fixture);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        await AddOneRuleAsync(fixture, home.OwnerToken, home.Rules);

        await ListRulesAsync(fixture, strangerToken, home.Rules, expectedStatus: 404);
        await ListRulesAsync(fixture, strangerToken, ChildRules(home.Child.Id), expectedStatus: 404);
        await ListRulesAsync(fixture, home.OwnerToken, GroupRules(Guid.CreateVersion7()), expectedStatus: 404);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {home.OwnerToken}");
            _.Delete.Url($"/groups/{home.GroupId}");
            _.StatusCodeShouldBe(204);
        });

        await ListRulesAsync(fixture, home.OwnerToken, home.Rules, expectedStatus: 404);
        await ListRulesAsync(fixture, home.ChildToken, home.Rules, expectedStatus: 404);
    }

    [Fact]
    public async Task A_guardian_whose_link_is_revoked_loses_the_personal_book_immediately()
    {
        var family = await WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);
        await AddOneRuleAsync(fixture, family.FirstToken, ChildRules(family.Child.Id));
        await ListRulesAsync(fixture, family.SecondToken, ChildRules(family.Child.Id));

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {family.SecondToken}");
            _.Delete.Url($"/users/me/children/{family.Child.Id}/guardian-link");
            _.StatusCodeShouldBe(204);
        });

        await ListRulesAsync(fixture, family.SecondToken, ChildRules(family.Child.Id), expectedStatus: 404);
    }
}
