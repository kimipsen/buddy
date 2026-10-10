using System.Text.Json;

using buddy.Features.Groups;
using buddy.Features.HouseRules;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.HouseRules.HouseRulesTestHelpers;

namespace buddy.IntegrationTests.Features.HouseRules.AcknowledgeRule;

[Collection(BuddyApiCollection.Name)]
public sealed class AcknowledgeRuleTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("AcknowledgeRule")]
    public async Task A_child_acknowledges_a_rule_and_repeating_it_appends_nothing()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var rule = await AddOneRuleAsync(fixture, token, ChildRules(child.Id));

        await AcknowledgeAsync(fixture, childToken, ChildRules(child.Id), rule.Id, 1);

        var book = await ListRulesAsync(fixture, token, ChildRules(child.Id));
        Assert.Equal(new RuleAcknowledgementDto(child.Id, 1, true), Assert.Single(Assert.Single(book!.Rules).Acknowledgements));

        var books = fixture.Host.Services.GetRequiredService<IRuleBookEventStore>();
        var bookId = RuleBookId.ForChild(new UserId(child.Id));
        var events = await books.ReadAsync(bookId, CancellationToken.None);
        var acknowledged = Assert.IsType<RuleAcknowledged>(events.Last().Value);
        Assert.Equal(child.Id, acknowledged.RecordedBy.Value);

        await AcknowledgeAsync(fixture, childToken, ChildRules(child.Id), rule.Id, 1);
        Assert.Equal(events.Count, (await books.ReadAsync(bookId, CancellationToken.None)).Count);
    }

    [Fact]
    public async Task Acknowledging_a_revision_from_before_a_normal_edit_is_a_conflict()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var rule = await AddOneRuleAsync(fixture, token, ChildRules(child.Id));
        await EditRuleAsync(fixture, token, ChildRules(child.Id), rule.Id, rule.Title, "- 30 min");

        var response = await AcknowledgeAsync(fixture, childToken, ChildRules(child.Id), rule.Id, 1, expectedStatus: 409);

        using var json = JsonDocument.Parse(await response.ReadAsTextAsync());
        Assert.Equal("house_rule_revision_changed", json.RootElement.GetProperty("code").GetString());

        await AcknowledgeAsync(fixture, childToken, ChildRules(child.Id), rule.Id, 2);
    }

    [Fact]
    public async Task A_minor_edit_since_reading_does_not_invalidate_the_acknowledgement()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var rule = await AddOneRuleAsync(fixture, token, ChildRules(child.Id), "Scren time");
        await EditRuleAsync(fixture, token, ChildRules(child.Id), rule.Id, "Screen time", rule.Body, requireReacknowledgement: false);

        await AcknowledgeAsync(fixture, childToken, ChildRules(child.Id), rule.Id, 1);

        var status = Assert.Single(Assert.Single((await ListRulesAsync(fixture, token, ChildRules(child.Id)))!.Rules).Acknowledgements);
        Assert.Equal(new RuleAcknowledgementDto(child.Id, 1, true), status);
    }

    [Fact]
    public async Task A_revision_that_does_not_exist_yet_or_is_below_one_is_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var rule = await AddOneRuleAsync(fixture, token, ChildRules(child.Id));

        await AcknowledgeAsync(fixture, childToken, ChildRules(child.Id), rule.Id, 2, expectedStatus: 400);
        await AcknowledgeAsync(fixture, childToken, ChildRules(child.Id), rule.Id, 0, expectedStatus: 400);
        await AcknowledgeAsync(fixture, childToken, ChildRules(child.Id), Guid.CreateVersion7(), 1, expectedStatus: 404);
    }

    [Fact]
    public async Task A_guardian_acknowledges_on_behalf_of_their_child_only_when_naming_them()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var sibling = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Ida");
        var rule = await AddOneRuleAsync(fixture, token, ChildRules(child.Id));

        await AcknowledgeAsync(fixture, token, ChildRules(child.Id), rule.Id, 1, expectedStatus: 400);
        // The sibling isn't in this personal book's scope.
        await AcknowledgeAsync(fixture, token, ChildRules(child.Id), rule.Id, 1, sibling.Id, expectedStatus: 403);

        await AcknowledgeAsync(fixture, token, ChildRules(child.Id), rule.Id, 1, child.Id);

        var books = fixture.Host.Services.GetRequiredService<IRuleBookEventStore>();
        var events = await books.ReadAsync(RuleBookId.ForChild(new UserId(child.Id)), CancellationToken.None);
        var acknowledged = Assert.IsType<RuleAcknowledged>(events.Last().Value);
        Assert.Equal(child.Id, acknowledged.ChildId.Value);
        Assert.Equal(guardianId, acknowledged.RecordedBy.Value);
    }

    [Fact]
    public async Task A_child_cannot_acknowledge_for_someone_else_and_strangers_get_not_found()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var sibling = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Ida");
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var rule = await AddOneRuleAsync(fixture, token, ChildRules(child.Id));

        await AcknowledgeAsync(fixture, childToken, ChildRules(child.Id), rule.Id, 1, sibling.Id, expectedStatus: 403);
        await AcknowledgeAsync(fixture, strangerToken, ChildRules(child.Id), rule.Id, 1, expectedStatus: 404);
        await AcknowledgeAsync(fixture, childToken, ChildRules(child.Id), rule.Id, 1, child.Id);
    }

    [Fact]
    [CoversEndpoint("AcknowledgeRuleForGroup")]
    public async Task Household_members_acknowledge_for_themselves_and_adults_cannot()
    {
        var home = await CreateHouseholdAsync(fixture);
        var rule = await AddOneRuleAsync(fixture, home.OwnerToken, home.Rules, "Dinner");

        await AcknowledgeAsync(fixture, home.AdultToken, home.Rules, rule.Id, 1, expectedStatus: 403);
        await AcknowledgeAsync(fixture, home.AdultToken, home.Rules, rule.Id, 1, home.Child.Id, expectedStatus: 403);
        await AcknowledgeAsync(fixture, home.ChildToken, home.Rules, rule.Id, 1);

        var status = Assert.Single(Assert.Single((await ListRulesAsync(fixture, home.AdultToken, home.Rules))!.Rules).Acknowledgements);
        Assert.True(status.IsUpToDate);
    }

    [Fact]
    public async Task A_household_admin_acknowledges_only_for_children_they_are_a_guardian_of()
    {
        var home = await CreateHouseholdAsync(fixture);
        var (otherParent, otherParentToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var otherChild = await GuardianTestHelpers.CreateChildAsync(fixture, otherParentToken, "Noah");
        await GroupTestHelpers.AddMemberAsync(fixture, home.OwnerToken, home.GroupId, otherParentToken, otherParent.Email, GroupRole.Admin);
        await AddChildToGroupAsync(fixture, otherParentToken, home.GroupId, otherChild.Id);
        var rule = await AddOneRuleAsync(fixture, home.OwnerToken, home.Rules, "Dinner");

        await AcknowledgeAsync(fixture, home.OwnerToken, home.Rules, rule.Id, 1, otherChild.Id, expectedStatus: 403);
        await AcknowledgeAsync(fixture, otherParentToken, home.Rules, rule.Id, 1, home.Child.Id, expectedStatus: 403);

        await AcknowledgeAsync(fixture, otherParentToken, home.Rules, rule.Id, 1, otherChild.Id);
        await AcknowledgeAsync(fixture, home.OwnerToken, home.Rules, rule.Id, 1, home.Child.Id);
    }

    [Fact]
    public async Task A_child_removed_from_the_household_can_no_longer_acknowledge_its_rules()
    {
        var home = await CreateHouseholdAsync(fixture);
        var rule = await AddOneRuleAsync(fixture, home.OwnerToken, home.Rules, "Dinner");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {home.OwnerToken}");
            _.Delete.Url($"/groups/{home.GroupId}/members/{home.Child.Id}");
            _.StatusCodeShouldBe(204);
        });

        await AcknowledgeAsync(fixture, home.ChildToken, home.Rules, rule.Id, 1, expectedStatus: 404);
    }
}
