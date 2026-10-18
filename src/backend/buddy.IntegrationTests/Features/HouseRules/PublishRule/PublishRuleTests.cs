using buddy.Features.HouseRules;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.HouseRules.HouseRulesTestHelpers;

namespace buddy.IntegrationTests.Features.HouseRules.PublishRule;

[Collection(BuddyApiCollection.Name)]
public sealed class PublishRuleTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("PublishRule")]
    public async Task Publishing_a_new_rule_shows_it_to_the_child_as_new()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Emil");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var draft = await AddOneRuleAsync(fixture, token, ChildRules(child.Id), "Bedtime", "Lights out at 20:00", publish: false);

        var book = await PublishRuleAsync(fixture, token, ChildRules(child.Id), draft.Id);

        var rule = Assert.Single(book!.Rules);
        Assert.True(rule.IsPublished);
        Assert.Null(rule.Draft);
        Assert.Equal("Bedtime", rule.Title);
        Assert.Equal("Lights out at 20:00", rule.Body);
        Assert.Equal(1, rule.Revision);
        Assert.Equal(1, rule.AcknowledgementRevision);
        Assert.Equal(new RuleAcknowledgementDto(child.Id, null, false), Assert.Single(rule.Acknowledgements));

        var childView = await GetChildRulesAsync(fixture, childToken, child.Id);
        Assert.Equal("Bedtime", Assert.Single(childView!.Personal.Rules).Title);
        Assert.Equal(1, childView.PendingAcknowledgements);
    }

    [Fact]
    public async Task A_first_publish_asks_for_acknowledgement_even_when_the_draft_is_a_small_fix()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var draft = await AddOneRuleAsync(fixture, token, ChildRules(child.Id), "Bedtim", publish: false);
        await EditRuleAsync(fixture, token, ChildRules(child.Id), draft.Id, "Bedtime", requireReacknowledgement: false);

        var book = await PublishRuleAsync(fixture, token, ChildRules(child.Id), draft.Id);

        var rule = Assert.Single(book!.Rules);
        Assert.Equal(1, rule.Revision);
        Assert.Equal(1, rule.AcknowledgementRevision);
        Assert.False(Assert.Single(rule.Acknowledgements).IsUpToDate);
    }

    [Fact]
    public async Task Publishing_a_rule_with_no_draft_appends_nothing()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var rule = await AddOneRuleAsync(fixture, token, ChildRules(child.Id));
        var books = fixture.Host.Services.GetRequiredService<IRuleBookEventStore>();
        var bookId = RuleBookId.ForChild(new UserId(child.Id));
        var before = (await books.ReadAsync(bookId, CancellationToken.None)).Count;

        var book = await PublishRuleAsync(fixture, token, ChildRules(child.Id), rule.Id);

        Assert.Equal(1, Assert.Single(book!.Rules).Revision);
        Assert.Equal(before, (await books.ReadAsync(bookId, CancellationToken.None)).Count);
    }

    [Fact]
    [CoversEndpoint("PublishRuleForGroup")]
    public async Task Only_a_household_admin_may_publish_and_strangers_get_not_found()
    {
        var home = await CreateHouseholdAsync(fixture);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var draft = await AddOneRuleAsync(fixture, home.OwnerToken, home.Rules, "Dinner", publish: false);

        await PublishRuleAsync(fixture, home.AdultToken, home.Rules, draft.Id, expectedStatus: 403);
        await PublishRuleAsync(fixture, home.ChildToken, home.Rules, draft.Id, expectedStatus: 403);
        await PublishRuleAsync(fixture, strangerToken, home.Rules, draft.Id, expectedStatus: 404);
        await PublishRuleAsync(fixture, home.OwnerToken, home.Rules, Guid.CreateVersion7(), expectedStatus: 404);
        Assert.Empty((await ListRulesAsync(fixture, home.ChildToken, home.Rules))!.Rules);

        var book = await PublishRuleAsync(fixture, home.OwnerToken, home.Rules, draft.Id);

        Assert.True(Assert.Single(book!.Rules).IsPublished);
        Assert.Equal("Dinner", Assert.Single((await ListRulesAsync(fixture, home.ChildToken, home.Rules))!.Rules).Title);
    }
}
