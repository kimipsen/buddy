using buddy.Features.HouseRules;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.HouseRules.HouseRulesTestHelpers;

namespace buddy.IntegrationTests.Features.HouseRules.ReorderRules;

[Collection(BuddyApiCollection.Name)]
public sealed class ReorderRulesTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ReorderRules")]
    public async Task A_guardian_reorders_rules_and_the_same_order_again_appends_nothing()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var a = await AddOneRuleAsync(fixture, token, ChildRules(child.Id), "A");
        var b = await AddOneRuleAsync(fixture, token, ChildRules(child.Id), "B");
        var c = await AddOneRuleAsync(fixture, token, ChildRules(child.Id), "C");

        var book = await ReorderRulesAsync(fixture, token, ChildRules(child.Id), [c.Id, a.Id, b.Id]);
        Assert.Equal(["C", "A", "B"], book!.Rules.Select(r => r.Title));

        var books = fixture.Host.Services.GetRequiredService<IRuleBookEventStore>();
        var bookId = RuleBookId.ForChild(new UserId(child.Id));
        var count = (await books.ReadAsync(bookId, CancellationToken.None)).Count;

        await ReorderRulesAsync(fixture, token, ChildRules(child.Id), [c.Id, a.Id, b.Id]);
        Assert.Equal(count, (await books.ReadAsync(bookId, CancellationToken.None)).Count);
        Assert.Equal(["C", "A", "B"], (await ListRulesAsync(fixture, token, ChildRules(child.Id)))!.Rules.Select(r => r.Title));
    }

    [Fact]
    public async Task An_order_that_is_not_a_permutation_of_the_current_rules_is_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var a = await AddOneRuleAsync(fixture, token, ChildRules(child.Id), "A");
        var b = await AddOneRuleAsync(fixture, token, ChildRules(child.Id), "B");

        await ReorderRulesAsync(fixture, token, ChildRules(child.Id), [a.Id], expectedStatus: 400);
        await ReorderRulesAsync(fixture, token, ChildRules(child.Id), [a.Id, b.Id, Guid.CreateVersion7()], expectedStatus: 400);
        await ReorderRulesAsync(fixture, token, ChildRules(child.Id), [a.Id, a.Id], expectedStatus: 400);
    }

    [Fact]
    public async Task An_empty_order_for_a_book_that_was_never_started_succeeds()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);

        var book = await ReorderRulesAsync(fixture, token, ChildRules(child.Id), []);

        Assert.Empty(book!.Rules);
    }

    [Fact]
    [CoversEndpoint("ReorderRulesForGroup")]
    public async Task Only_a_household_admin_reorders_group_rules()
    {
        var home = await CreateHouseholdAsync(fixture);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var a = await AddOneRuleAsync(fixture, home.OwnerToken, home.Rules, "A");
        var b = await AddOneRuleAsync(fixture, home.OwnerToken, home.Rules, "B");

        await ReorderRulesAsync(fixture, home.AdultToken, home.Rules, [b.Id, a.Id], expectedStatus: 403);
        await ReorderRulesAsync(fixture, home.ChildToken, home.Rules, [b.Id, a.Id], expectedStatus: 403);
        await ReorderRulesAsync(fixture, strangerToken, home.Rules, [b.Id, a.Id], expectedStatus: 404);

        var book = await ReorderRulesAsync(fixture, home.OwnerToken, home.Rules, [b.Id, a.Id]);
        Assert.Equal(["B", "A"], book!.Rules.Select(r => r.Title));
    }
}
