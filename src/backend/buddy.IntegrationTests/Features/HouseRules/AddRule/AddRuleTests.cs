using buddy.Features.HouseRules;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.HouseRules.HouseRulesTestHelpers;

namespace buddy.IntegrationTests.Features.HouseRules.AddRule;

[Collection(BuddyApiCollection.Name)]
public sealed class AddRuleTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("AddRule")]
    public async Task A_guardian_adds_a_personal_rule_and_the_book_is_started_on_first_use()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Emil");
        var books = fixture.Host.Services.GetRequiredService<IRuleBookEventStore>();
        var bookId = RuleBookId.ForChild(new UserId(child.Id));

        Assert.Empty(await books.ReadAsync(bookId, CancellationToken.None));

        var book = await AddRuleAsync(fixture, token, ChildRules(child.Id), "  Screen time  ", "| Day | Time |\n|---|---|\n| Mon | 45 min |");

        Assert.Equal("Child", book!.ScopeKind);
        Assert.Equal(child.Id, book.ScopeId);
        Assert.Equal("Manage", book.Access);
        Assert.Equal([child.Id], book.Children);
        var rule = Assert.Single(book.Rules);
        Assert.Equal("Screen time", rule.Title);
        Assert.Equal("| Day | Time |\n|---|---|\n| Mon | 45 min |", rule.Body);
        Assert.Equal(1, rule.Revision);
        Assert.Equal(1, rule.AcknowledgementRevision);
        var acknowledgement = Assert.Single(rule.Acknowledgements);
        Assert.Equal(new RuleAcknowledgementDto(child.Id, null, false), acknowledgement);

        var events = await books.ReadAsync(bookId, CancellationToken.None);
        Assert.Collection(events, e => Assert.IsType<RuleBookStarted>(e.Value), e => Assert.IsType<RuleAdded>(e.Value));

        var second = await AddRuleAsync(fixture, token, ChildRules(child.Id), "Bedtime");
        Assert.Equal(["Screen time", "Bedtime"], second!.Rules.Select(r => r.Title));
        Assert.Equal(3, (await books.ReadAsync(bookId, CancellationToken.None)).Count);
    }

    [Fact]
    [CoversEndpoint("AddRuleForGroup")]
    public async Task A_household_owner_adds_a_rule_that_tracks_every_child_member()
    {
        var home = await CreateHouseholdAsync(fixture);

        var book = await AddRuleAsync(fixture, home.OwnerToken, home.Rules, "No phones at the dinner table");

        Assert.Equal("Group", book!.ScopeKind);
        Assert.Equal(home.GroupId, book.ScopeId);
        Assert.Equal([home.Child.Id], book.Children);
        Assert.Equal(home.Child.Id, Assert.Single(Assert.Single(book.Rules).Acknowledgements).ChildId);
    }

    [Fact]
    public async Task A_title_only_rule_is_allowed_and_markdown_is_stored_exactly_as_typed()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        const string unsafeBody = "<script>alert(1)</script> [x](javascript:alert(1))";

        const string indentedBody = "    a code block\nline break  ";

        await AddRuleAsync(fixture, token, ChildRules(child.Id), "Shoes off at the door", "  \n ");
        await AddRuleAsync(fixture, token, ChildRules(child.Id), "Links", unsafeBody);
        var book = await AddRuleAsync(fixture, token, ChildRules(child.Id), "Code", indentedBody);

        Assert.Equal("", book!.Rules[0].Body);
        Assert.Equal(unsafeBody, book.Rules[1].Body);
        Assert.Equal(indentedBody, book.Rules[2].Body);
    }

    [Theory]
    [InlineData("   ", "")]
    [InlineData(null, "")]
    [InlineData("101", "")]
    [InlineData("Ok", "4001")]
    public async Task Invalid_content_is_rejected(string? title, string body)
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(new { Title = title == "101" ? new string('a', 101) : title, Body = body == "4001" ? new string('b', 4001) : body })
                .ToUrl(ChildRules(child.Id));
            _.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task The_fifty_first_rule_is_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);

        for (var i = 1; i <= RuleBook.MaxRules; i++)
        {
            await AddRuleAsync(fixture, token, ChildRules(child.Id), $"Rule {i}");
        }

        await AddRuleAsync(fixture, token, ChildRules(child.Id), "One too many", expectedStatus: 400);
    }

    [Fact]
    public async Task A_child_can_read_but_not_write_their_own_rules()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        await AddRuleAsync(fixture, childToken, ChildRules(child.Id), "No homework", expectedStatus: 403);
    }

    [Fact]
    public async Task Only_a_household_admin_may_add_and_strangers_get_not_found()
    {
        var home = await CreateHouseholdAsync(fixture);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await AddRuleAsync(fixture, home.AdultToken, home.Rules, "Grandma's rule", expectedStatus: 403);
        await AddRuleAsync(fixture, home.ChildToken, home.Rules, "No bedtime", expectedStatus: 403);
        await AddRuleAsync(fixture, strangerToken, home.Rules, "Hello", expectedStatus: 404);
        await AddRuleAsync(fixture, strangerToken, ChildRules(home.Child.Id), "Hello", expectedStatus: 404);
        await AddRuleAsync(fixture, home.OwnerToken, GroupRules(Guid.CreateVersion7()), "Missing group", expectedStatus: 404);
    }
}
