using buddy.Features.HouseRules;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.HouseRules.HouseRulesTestHelpers;

namespace buddy.IntegrationTests.Features.HouseRules.DiscardRuleDraft;

[Collection(BuddyApiCollection.Name)]
public sealed class DiscardRuleDraftTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("DiscardRuleDraft")]
    public async Task Discarding_a_draft_goes_back_to_the_published_text()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var rule = await AddOneRuleAsync(fixture, token, ChildRules(child.Id), "Screen time", "- 45 min");
        await EditRuleAsync(fixture, token, ChildRules(child.Id), rule.Id, "Screen time", "- 30 min");

        await DiscardRuleDraftAsync(fixture, token, ChildRules(child.Id), rule.Id);

        var after = Assert.Single((await ListRulesAsync(fixture, token, ChildRules(child.Id)))!.Rules);
        Assert.Null(after.Draft);
        Assert.Equal("- 45 min", after.Body);
        Assert.Equal(1, after.Revision);
    }

    [Fact]
    public async Task Discarding_when_there_is_no_draft_appends_nothing()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var rule = await AddOneRuleAsync(fixture, token, ChildRules(child.Id));
        var books = fixture.Host.Services.GetRequiredService<IRuleBookEventStore>();
        var bookId = RuleBookId.ForChild(new UserId(child.Id));
        var before = (await books.ReadAsync(bookId, CancellationToken.None)).Count;

        await DiscardRuleDraftAsync(fixture, token, ChildRules(child.Id), rule.Id);

        Assert.Equal(before, (await books.ReadAsync(bookId, CancellationToken.None)).Count);
    }

    [Fact]
    public async Task A_never_published_rule_has_no_draft_to_discard()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var rule = await AddOneRuleAsync(fixture, token, ChildRules(child.Id), publish: false);

        await DiscardRuleDraftAsync(fixture, token, ChildRules(child.Id), rule.Id, expectedStatus: 400);

        Assert.NotNull(Assert.Single((await ListRulesAsync(fixture, token, ChildRules(child.Id)))!.Rules).Draft);
    }

    [Fact]
    [CoversEndpoint("DiscardRuleDraftForGroup")]
    public async Task Only_a_household_admin_may_discard_and_strangers_get_not_found()
    {
        var home = await CreateHouseholdAsync(fixture);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var rule = await AddOneRuleAsync(fixture, home.OwnerToken, home.Rules, "Dinner");
        await EditRuleAsync(fixture, home.OwnerToken, home.Rules, rule.Id, "Dinner", "Phones in the basket");

        await DiscardRuleDraftAsync(fixture, home.AdultToken, home.Rules, rule.Id, expectedStatus: 403);
        await DiscardRuleDraftAsync(fixture, home.ChildToken, home.Rules, rule.Id, expectedStatus: 403);
        await DiscardRuleDraftAsync(fixture, strangerToken, home.Rules, rule.Id, expectedStatus: 404);
        await DiscardRuleDraftAsync(fixture, home.OwnerToken, home.Rules, Guid.CreateVersion7(), expectedStatus: 404);

        await DiscardRuleDraftAsync(fixture, home.OwnerToken, home.Rules, rule.Id);

        Assert.Null(Assert.Single((await ListRulesAsync(fixture, home.OwnerToken, home.Rules))!.Rules).Draft);
    }
}
