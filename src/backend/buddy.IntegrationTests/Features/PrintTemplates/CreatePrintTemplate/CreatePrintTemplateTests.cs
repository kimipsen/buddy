using buddy.Features.Groups;
using buddy.Features.PrintTemplates;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.PrintTemplates.CreatePrintTemplate;

[Collection(BuddyApiCollection.Name)]
public sealed class CreatePrintTemplateTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("CreatePrintTemplate")]
    public async Task A_guardian_creates_a_personal_template_with_the_defaults()
    {
        var (_, token, userId) = await fixture.CreateAuthenticatedUserAsync();

        var template = await PrintTemplateTestHelpers.CreateAsync(fixture, token, "  Ugeplan  ");

        Assert.Equal("Ugeplan", template.Name);
        Assert.Equal(userId, template.OwnerUserId);
        Assert.Null(template.OwnerGroupId);
        Assert.Equal(PaperSize.A4, template.PaperSize);
        Assert.Equal(DayOfWeek.Monday, template.DefaultStartWeekday);
        Assert.True(template.ShowWeekNumber);
        Assert.Empty(template.Rows);
        Assert.Empty(template.GuardianColors);
        Assert.Empty(template.BabysitterColors);
    }

    [Fact]
    public async Task A_group_member_creates_a_template_owned_by_the_group()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var (member, memberToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Familien");
        await GroupTestHelpers.AddMemberAsync(fixture, ownerToken, groupId, memberToken, member.Email, GroupRole.Member);

        var template = await PrintTemplateTestHelpers.CreateAsync(fixture, memberToken, "Fælles", groupId);

        Assert.Equal(groupId, template.OwnerGroupId);
        Assert.Null(template.OwnerUserId);
    }

    [Fact]
    public async Task A_group_the_caller_is_not_a_member_of_is_not_found()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Familien");
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();

        foreach (var target in new[] { groupId, Guid.NewGuid() })
        {
            await fixture.Host.Scenario(_ =>
            {
                _.WithRequestHeader("Authorization", $"Bearer {strangerToken}");
                _.Post.Json(new { Name = "Ugeplan", GroupId = target }).ToUrl("/print-templates");
                _.StatusCodeShouldBe(404);
            });
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A template name that is much longer than the eighty characters a template name may have")]
    public async Task A_blank_or_oversized_name_is_rejected(string name)
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(new { Name = name }).ToUrl("/print-templates");
            _.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task A_child_account_is_forbidden()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Post.Json(new { Name = "Min plan" }).ToUrl("/print-templates");
            _.StatusCodeShouldBe(403);
        });
    }
}
