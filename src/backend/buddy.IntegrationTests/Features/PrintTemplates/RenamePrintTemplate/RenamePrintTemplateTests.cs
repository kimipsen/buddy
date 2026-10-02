using Alba;

using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.PrintTemplates.RenamePrintTemplate;

[Collection(BuddyApiCollection.Name)]
public sealed class RenamePrintTemplateTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("RenamePrintTemplate")]
    public async Task Renaming_updates_the_template_and_the_list()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var created = await PrintTemplateTestHelpers.CreateAsync(fixture, token, "Ugeplan");

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Name = " Skoleuge " }).ToUrl($"/print-templates/{created.Id}/name");
            _.StatusCodeShouldBeOk();
        });

        Assert.Equal("Skoleuge", response.ReadAsJson<PrintTemplateDto>().Name);
        Assert.Equal("Skoleuge", Assert.Single(await PrintTemplateTestHelpers.ListAsync(fixture, token)).Name);
    }

    [Fact]
    public async Task A_blank_name_is_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var created = await PrintTemplateTestHelpers.CreateAsync(fixture, token);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Name = " " }).ToUrl($"/print-templates/{created.Id}/name");
            _.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task A_stranger_gets_not_found()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var created = await PrintTemplateTestHelpers.CreateAsync(fixture, ownerToken);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {strangerToken}");
            _.Patch.Json(new { Name = "Mine now" }).ToUrl($"/print-templates/{created.Id}/name");
            _.StatusCodeShouldBe(404);
        });
    }
}
