using Alba;

using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.TaskLibrary;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.TaskLibrary.ListTaskTemplates;

[Collection(BuddyApiCollection.Name)]
public sealed class ListTaskTemplatesTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ListTaskTemplates")]
    public async Task Lists_every_task_template_created_for_the_child()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        await TaskLibraryTestHelpers.CreateTaskTemplateAsync(fixture, guardianToken, child.Id, new CreateTaskTemplateOptions(Name: "Morning routine"));
        await TaskLibraryTestHelpers.CreateTaskTemplateAsync(fixture, guardianToken, child.Id, new CreateTaskTemplateOptions(Name: "Bedtime routine"));

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/task-templates/children/{child.Id}");
            _.StatusCodeShouldBeOk();
        });

        var templates = response.ReadAsJson<List<TaskTemplateDto>>();
        Assert.Equal(2, templates.Count);
        Assert.Contains(templates, t => t.Name == "Morning routine");
        Assert.Contains(templates, t => t.Name == "Bedtime routine");
    }

    [Fact]
    public async Task The_child_can_also_list_their_own_task_templates()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        await TaskLibraryTestHelpers.CreateTaskTemplateAsync(fixture, guardianToken, child.Id, new CreateTaskTemplateOptions(Name: "Morning routine"));

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Get.Url($"/task-templates/children/{child.Id}");
            _.StatusCodeShouldBeOk();
        });

        Assert.Single(response.ReadAsJson<List<TaskTemplateDto>>());
    }

    [Fact]
    public async Task A_third_party_with_no_guardian_link_gets_not_found()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {strangerToken}");
            _.Get.Url($"/task-templates/children/{child.Id}");
            _.StatusCodeShouldBe(404);
        });
    }
}
