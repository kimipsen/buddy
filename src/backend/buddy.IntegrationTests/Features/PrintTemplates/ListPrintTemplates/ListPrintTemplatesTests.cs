using Alba;

using buddy.Features.Groups;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.PrintTemplates.ListPrintTemplates;

[Collection(BuddyApiCollection.Name)]
public sealed class ListPrintTemplatesTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ListPrintTemplates")]
    public async Task A_guardian_sees_their_own_and_their_groups_templates_but_no_one_elses()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var (member, memberToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Familien");
        await GroupTestHelpers.AddMemberAsync(fixture, ownerToken, groupId, memberToken, member.Email, GroupRole.Member);

        var mine = await PrintTemplateTestHelpers.CreateAsync(fixture, memberToken, "B mine");
        var shared = await PrintTemplateTestHelpers.CreateAsync(fixture, ownerToken, "A shared", groupId);
        var ownersPrivate = await PrintTemplateTestHelpers.CreateAsync(fixture, ownerToken, "C private");

        var listed = await PrintTemplateTestHelpers.ListAsync(fixture, memberToken);

        Assert.Equal([shared.Id, mine.Id], listed.Select(t => t.Id));
        Assert.DoesNotContain(listed, t => t.Id == ownersPrivate.Id);
    }

    [Fact]
    public async Task Deleted_templates_are_not_listed()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var template = await PrintTemplateTestHelpers.CreateAsync(fixture, token);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url($"/print-templates/{template.Id}");
            _.StatusCodeShouldBe(204);
        });

        Assert.Empty(await PrintTemplateTestHelpers.ListAsync(fixture, token));
    }

    [Fact]
    public async Task A_child_in_the_owning_group_sees_nothing()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, guardianToken, "Familien");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Url($"/groups/{groupId}/children/{child.Id}");
            _.StatusCodeShouldBe(204);
        });
        await PrintTemplateTestHelpers.CreateAsync(fixture, guardianToken, "Fælles", groupId);

        Assert.Empty(await PrintTemplateTestHelpers.ListAsync(fixture, childToken));
    }
}
