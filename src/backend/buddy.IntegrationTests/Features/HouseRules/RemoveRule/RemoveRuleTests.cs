using buddy.Features.HouseRules;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.HouseRules.HouseRulesTestHelpers;

namespace buddy.IntegrationTests.Features.HouseRules.RemoveRule;

[Collection(BuddyApiCollection.Name)]
public sealed class RemoveRuleTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("RemoveRule")]
    public async Task Removing_a_rule_drops_it_and_its_acknowledgements_and_repeating_it_appends_nothing()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var rule = await AddOneRuleAsync(fixture, token, ChildRules(child.Id), "Bedtime");
        var kept = await AddOneRuleAsync(fixture, token, ChildRules(child.Id), "Dinner");
        await AcknowledgeAsync(fixture, childToken, ChildRules(child.Id), rule.Id, 1);

        await RemoveRuleAsync(fixture, token, ChildRules(child.Id), rule.Id);

        var book = await ListRulesAsync(fixture, token, ChildRules(child.Id));
        Assert.Equal([kept.Id], book!.Rules.Select(r => r.Id));

        var books = fixture.Host.Services.GetRequiredService<IRuleBookEventStore>();
        var bookId = RuleBookId.ForChild(new UserId(child.Id));
        var snapshot = await books.FindSnapshotAsync(bookId, CancellationToken.None);
        Assert.DoesNotContain(snapshot!.Acknowledgements.Keys, k => k.Rule.Value == rule.Id);

        var count = (await books.ReadAsync(bookId, CancellationToken.None)).Count;
        await RemoveRuleAsync(fixture, token, ChildRules(child.Id), rule.Id);
        Assert.Equal(count, (await books.ReadAsync(bookId, CancellationToken.None)).Count);
    }

    [Fact]
    public async Task Removing_from_a_book_that_was_never_started_succeeds()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);

        await RemoveRuleAsync(fixture, token, ChildRules(child.Id), Guid.CreateVersion7());
    }

    [Fact]
    [CoversEndpoint("RemoveRuleForGroup")]
    public async Task Only_a_household_admin_removes_group_rules()
    {
        var home = await CreateHouseholdAsync(fixture);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var rule = await AddOneRuleAsync(fixture, home.OwnerToken, home.Rules);

        await RemoveRuleAsync(fixture, home.AdultToken, home.Rules, rule.Id, expectedStatus: 403);
        await RemoveRuleAsync(fixture, home.ChildToken, home.Rules, rule.Id, expectedStatus: 403);
        await RemoveRuleAsync(fixture, strangerToken, home.Rules, rule.Id, expectedStatus: 404);
        await RemoveRuleAsync(fixture, home.OwnerToken, home.Rules, rule.Id);

        Assert.Empty((await ListRulesAsync(fixture, home.OwnerToken, home.Rules))!.Rules);
    }
}
