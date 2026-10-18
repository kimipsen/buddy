using buddy.Features.HouseRules;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.HouseRules.HouseRulesTestHelpers;

namespace buddy.IntegrationTests.Features.HouseRules.EditRule;

[Collection(BuddyApiCollection.Name)]
public sealed class EditRuleTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("EditRule")]
    public async Task An_edit_saves_a_draft_and_the_child_keeps_reading_the_published_text()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var rule = await AddOneRuleAsync(fixture, token, ChildRules(child.Id));
        await AcknowledgeAsync(fixture, childToken, ChildRules(child.Id), rule.Id, 1);

        var book = await EditRuleAsync(fixture, token, ChildRules(child.Id), rule.Id, "Screen time", "- 30 min");

        var edited = Assert.Single(book!.Rules);
        Assert.Equal("- 45 min", edited.Body);
        Assert.Equal(1, edited.Revision);
        Assert.Equal(new RuleDraftDto("Screen time", "- 30 min", true, edited.Draft!.EditedAt), edited.Draft);
        Assert.True(Assert.Single(edited.Acknowledgements).IsUpToDate);

        var childView = await GetChildRulesAsync(fixture, childToken, child.Id);
        var seen = Assert.Single(childView!.Personal.Rules);
        Assert.Equal("- 45 min", seen.Body);
        Assert.True(seen.IsUpToDate);
        var listed = Assert.Single((await ListRulesAsync(fixture, childToken, ChildRules(child.Id)))!.Rules);
        Assert.Equal("- 45 min", listed.Body);
        Assert.Null(listed.Draft);
    }

    [Fact]
    public async Task A_normal_edit_asks_the_child_to_acknowledge_again_once_published()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var rule = await AddOneRuleAsync(fixture, token, ChildRules(child.Id));
        await AcknowledgeAsync(fixture, childToken, ChildRules(child.Id), rule.Id, 1);

        var book = await EditAndPublishRuleAsync(fixture, token, ChildRules(child.Id), rule.Id, "Screen time", "- 30 min");

        var edited = Assert.Single(book.Rules);
        Assert.Equal("- 30 min", edited.Body);
        Assert.Equal(2, edited.Revision);
        Assert.Equal(2, edited.AcknowledgementRevision);
        Assert.Null(edited.Draft);
        Assert.Equal(new RuleAcknowledgementDto(child.Id, 1, false), Assert.Single(edited.Acknowledgements));
    }

    [Fact]
    public async Task A_minor_edit_keeps_the_childs_acknowledgement_once_published()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var rule = await AddOneRuleAsync(fixture, token, ChildRules(child.Id), "Scren time");
        await AcknowledgeAsync(fixture, childToken, ChildRules(child.Id), rule.Id, 1);

        var draft = await EditRuleAsync(fixture, token, ChildRules(child.Id), rule.Id, "Screen time", rule.Body, requireReacknowledgement: false);
        Assert.False(Assert.Single(draft!.Rules).Draft!.RequiresReacknowledgement);

        var book = await PublishRuleAsync(fixture, token, ChildRules(child.Id), rule.Id);

        var edited = Assert.Single(book!.Rules);
        Assert.Equal("Screen time", edited.Title);
        Assert.Equal(2, edited.Revision);
        Assert.Equal(1, edited.AcknowledgementRevision);
        Assert.True(Assert.Single(edited.Acknowledgements).IsUpToDate);
    }

    [Fact]
    public async Task Saving_unchanged_content_appends_nothing()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var rule = await AddOneRuleAsync(fixture, token, ChildRules(child.Id));
        var books = fixture.Host.Services.GetRequiredService<IRuleBookEventStore>();
        var bookId = RuleBookId.ForChild(new UserId(child.Id));
        var before = (await books.ReadAsync(bookId, CancellationToken.None)).Count;

        var book = await EditRuleAsync(fixture, token, ChildRules(child.Id), rule.Id, rule.Title, rule.Body);

        Assert.Null(Assert.Single(book!.Rules).Draft);
        Assert.Equal(before, (await books.ReadAsync(bookId, CancellationToken.None)).Count);

        await EditRuleAsync(fixture, token, ChildRules(child.Id), rule.Id, rule.Title, "- 30 min");
        var afterDraft = (await books.ReadAsync(bookId, CancellationToken.None)).Count;
        await EditRuleAsync(fixture, token, ChildRules(child.Id), rule.Id, rule.Title, "- 30 min");

        Assert.Equal(afterDraft, (await books.ReadAsync(bookId, CancellationToken.None)).Count);
    }

    [Fact]
    public async Task Changing_only_the_small_fix_switch_updates_the_draft()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var rule = await AddOneRuleAsync(fixture, token, ChildRules(child.Id));
        await EditRuleAsync(fixture, token, ChildRules(child.Id), rule.Id, rule.Title, "- 30 min");

        var book = await EditRuleAsync(fixture, token, ChildRules(child.Id), rule.Id, rule.Title, "- 30 min", requireReacknowledgement: false);

        Assert.False(Assert.Single(book!.Rules).Draft!.RequiresReacknowledgement);
    }

    [Fact]
    public async Task Restoring_the_published_text_discards_the_draft()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var rule = await AddOneRuleAsync(fixture, token, ChildRules(child.Id));
        await EditRuleAsync(fixture, token, ChildRules(child.Id), rule.Id, rule.Title, "- 30 min");

        var book = await EditRuleAsync(fixture, token, ChildRules(child.Id), rule.Id, rule.Title, rule.Body);

        Assert.Null(Assert.Single(book!.Rules).Draft);
        var books = fixture.Host.Services.GetRequiredService<IRuleBookEventStore>();
        var events = await books.ReadAsync(RuleBookId.ForChild(new UserId(child.Id)), CancellationToken.None);
        Assert.IsType<RuleDraftDiscarded>(events.Last().Value);
    }

    [Fact]
    public async Task Editing_a_never_published_rule_changes_its_draft()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var rule = await AddOneRuleAsync(fixture, token, ChildRules(child.Id), "Bedtim", publish: false);

        var book = await EditRuleAsync(fixture, token, ChildRules(child.Id), rule.Id, "Bedtime", "Lights out at 20:00");

        var edited = Assert.Single(book!.Rules);
        Assert.False(edited.IsPublished);
        Assert.Equal("Bedtime", edited.Draft!.Title);
        Assert.Equal("Lights out at 20:00", edited.Draft.Body);
    }

    [Fact]
    [CoversEndpoint("EditRuleForGroup")]
    public async Task A_household_admin_edits_a_group_rule_but_other_members_cannot()
    {
        var home = await CreateHouseholdAsync(fixture);
        var rule = await AddOneRuleAsync(fixture, home.OwnerToken, home.Rules, "Dinner");

        var book = await EditRuleAsync(fixture, home.OwnerToken, home.Rules, rule.Id, "Dinner", "Phones in the basket");
        Assert.Equal("Phones in the basket", Assert.Single(book!.Rules).Draft!.Body);

        await EditRuleAsync(fixture, home.AdultToken, home.Rules, rule.Id, "Dinner", "x", expectedStatus: 403);
        await EditRuleAsync(fixture, home.ChildToken, home.Rules, rule.Id, "Dinner", "x", expectedStatus: 403);
    }

    [Fact]
    public async Task Unknown_rules_invalid_content_and_strangers_are_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var rule = await AddOneRuleAsync(fixture, token, ChildRules(child.Id));

        await EditRuleAsync(fixture, token, ChildRules(child.Id), Guid.CreateVersion7(), "Missing", expectedStatus: 404);
        await EditRuleAsync(fixture, token, ChildRules(child.Id), rule.Id, " ", expectedStatus: 400);
        await EditRuleAsync(fixture, childToken, ChildRules(child.Id), rule.Id, "Mine now", expectedStatus: 403);
        await EditRuleAsync(fixture, strangerToken, ChildRules(child.Id), rule.Id, "Hello", expectedStatus: 404);
    }
}
