using buddy.Features.HouseRules;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.HouseRules.HouseRulesTestHelpers;

namespace buddy.IntegrationTests.Features.HouseRules.PublishRules;

[Collection(BuddyApiCollection.Name)]
public sealed class PublishRulesTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("PublishRules")]
    public async Task Publish_all_releases_every_draft_in_one_append()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var published = await AddOneRuleAsync(fixture, token, ChildRules(child.Id), "Dinner", "Phones away");
        var untouched = await AddOneRuleAsync(fixture, token, ChildRules(child.Id), "Shoes off");
        await AcknowledgeAsync(fixture, childToken, ChildRules(child.Id), published.Id, 1);
        await EditRuleAsync(fixture, token, ChildRules(child.Id), published.Id, "Dinner", "Phones in the basket");
        var draft = await AddOneRuleAsync(fixture, token, ChildRules(child.Id), "Sleepovers", publish: false);
        var books = fixture.Host.Services.GetRequiredService<IRuleBookEventStore>();
        var bookId = RuleBookId.ForChild(new UserId(child.Id));
        var before = (await books.ReadAsync(bookId, CancellationToken.None)).Count;

        var book = await PublishRulesAsync(fixture, token, ChildRules(child.Id));

        Assert.All(book!.Rules, rule => Assert.Null(rule.Draft));
        Assert.Equal(
            [(published.Id, 2, "Phones in the basket"), (untouched.Id, 1, "- 45 min"), (draft.Id, 1, "- 45 min")],
            book.Rules.Select(r => (r.Id, r.Revision, r.Body)));
        var events = await books.ReadAsync(bookId, CancellationToken.None);
        Assert.Equal(before + 2, events.Count);
        Assert.All(events.Skip(before), e => Assert.IsType<RulePublished>(e.Value));

        var childView = await GetChildRulesAsync(fixture, childToken, child.Id);
        Assert.Equal(["Dinner", "Shoes off", "Sleepovers"], childView!.Personal.Rules.Select(r => r.Title));
        // "Dinner" changed, "Shoes off" was never read, and "Sleepovers" is new.
        Assert.Equal(3, childView.PendingAcknowledgements);
    }

    [Fact]
    public async Task Publish_all_without_drafts_or_a_book_appends_nothing()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var books = fixture.Host.Services.GetRequiredService<IRuleBookEventStore>();
        var bookId = RuleBookId.ForChild(new UserId(child.Id));

        Assert.Empty((await PublishRulesAsync(fixture, token, ChildRules(child.Id)))!.Rules);
        Assert.Empty(await books.ReadAsync(bookId, CancellationToken.None));

        await AddOneRuleAsync(fixture, token, ChildRules(child.Id));
        var before = (await books.ReadAsync(bookId, CancellationToken.None)).Count;

        Assert.Single((await PublishRulesAsync(fixture, token, ChildRules(child.Id)))!.Rules);
        Assert.Equal(before, (await books.ReadAsync(bookId, CancellationToken.None)).Count);
    }

    [Fact]
    [CoversEndpoint("PublishRulesForGroup")]
    public async Task Only_a_household_admin_may_publish_all_and_strangers_get_not_found()
    {
        var home = await CreateHouseholdAsync(fixture);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        await AddOneRuleAsync(fixture, home.OwnerToken, home.Rules, "Dinner", publish: false);

        await PublishRulesAsync(fixture, home.AdultToken, home.Rules, expectedStatus: 403);
        await PublishRulesAsync(fixture, home.ChildToken, home.Rules, expectedStatus: 403);
        await PublishRulesAsync(fixture, strangerToken, home.Rules, expectedStatus: 404);
        await PublishRulesAsync(fixture, home.OwnerToken, GroupRules(Guid.CreateVersion7()), expectedStatus: 404);

        var book = await PublishRulesAsync(fixture, home.OwnerToken, home.Rules);

        Assert.True(Assert.Single(book!.Rules).IsPublished);
    }
}
