using Alba;

using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.PrintTemplates.DeletePrintTemplate;

[Collection(BuddyApiCollection.Name)]
public sealed class DeletePrintTemplateTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("DeletePrintTemplate")]
    public async Task A_deleted_template_is_gone_and_deleting_again_is_not_found()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var created = await PrintTemplateTestHelpers.CreateAsync(fixture, token);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url($"/print-templates/{created.Id}");
            _.StatusCodeShouldBe(204);
        });

        foreach (var request in new Action<Alba.Scenario>[]
        {
            _ => _.Get.Url($"/print-templates/{created.Id}"),
            _ => _.Delete.Url($"/print-templates/{created.Id}"),
        })
        {
            await fixture.Host.Scenario(_ =>
            {
                _.WithRequestHeader("Authorization", $"Bearer {token}");
                request(_);
                _.StatusCodeShouldBe(404);
            });
        }
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
            _.Delete.Url($"/print-templates/{created.Id}");
            _.StatusCodeShouldBe(404);
        });

        await PrintTemplateTestHelpers.GetAsync(fixture, ownerToken, created.Id);
    }
}
