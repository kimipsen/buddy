using Alba;

using buddy.Features.Groups;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.PrintTemplates.GetPrintTemplate;

[Collection(BuddyApiCollection.Name)]
public sealed class GetPrintTemplateTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("GetPrintTemplate")]
    public async Task Any_guardian_member_of_the_owning_group_can_read_a_group_template()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var (member, memberToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Familien");
        await GroupTestHelpers.AddMemberAsync(fixture, ownerToken, groupId, memberToken, member.Email, GroupRole.Member);
        var created = await PrintTemplateTestHelpers.CreateAsync(fixture, ownerToken, "Fælles", groupId);

        var read = await PrintTemplateTestHelpers.GetAsync(fixture, memberToken, created.Id);

        Assert.Equal(created.Id, read.Id);
        Assert.Equal("Fælles", read.Name);
    }

    [Fact]
    public async Task Someone_elses_personal_template_and_an_unknown_id_are_not_found()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var created = await PrintTemplateTestHelpers.CreateAsync(fixture, ownerToken);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();

        foreach (var (token, id) in new[] { (strangerToken, created.Id), (ownerToken, Guid.NewGuid()) })
        {
            await fixture.Host.Scenario(_ =>
            {
                _.WithRequestHeader("Authorization", $"Bearer {token}");
                _.Get.Url($"/print-templates/{id}");
                _.StatusCodeShouldBe(404);
            });
        }
    }

    [Fact]
    public async Task A_member_removed_from_the_group_loses_the_template()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var (member, memberToken, memberId) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Familien");
        await GroupTestHelpers.AddMemberAsync(fixture, ownerToken, groupId, memberToken, member.Email, GroupRole.Member);
        var created = await PrintTemplateTestHelpers.CreateAsync(fixture, ownerToken, "Fælles", groupId);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Delete.Url($"/groups/{groupId}/members/{memberId}");
            _.StatusCodeShouldBe(204);
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {memberToken}");
            _.Get.Url($"/print-templates/{created.Id}");
            _.StatusCodeShouldBe(404);
        });
        Assert.Empty(await PrintTemplateTestHelpers.ListAsync(fixture, memberToken));
    }

    [Fact]
    public async Task A_deleted_groups_templates_disappear()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Familien");
        var created = await PrintTemplateTestHelpers.CreateAsync(fixture, ownerToken, "Fælles", groupId);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Delete.Url($"/groups/{groupId}");
            _.StatusCodeShouldBe(204);
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Get.Url($"/print-templates/{created.Id}");
            _.StatusCodeShouldBe(404);
        });
        Assert.Empty(await PrintTemplateTestHelpers.ListAsync(fixture, ownerToken));
    }

    [Fact]
    public async Task A_child_in_the_owning_group_gets_not_found()
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
        var created = await PrintTemplateTestHelpers.CreateAsync(fixture, guardianToken, "Fælles", groupId);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Get.Url($"/print-templates/{created.Id}");
            _.StatusCodeShouldBe(404);
        });
    }
}
