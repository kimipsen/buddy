using Alba;

using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.TaskLibrary;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.TaskLibrary.ArchiveTaskTemplate;

[Collection(BuddyApiCollection.Name)]
public sealed class ArchiveTaskTemplateTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ArchiveTaskTemplate")]
    public async Task Archiving_a_task_template_keeps_it_in_the_library_but_blocks_further_writes()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var template = await TaskLibraryTestHelpers.CreateTaskTemplateAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(template);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Delete.Url($"/task-templates/{template.Id}");
            _.StatusCodeShouldBe(204);
        });

        var listResponse = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/task-templates/children/{child.Id}");
            _.StatusCodeShouldBeOk();
        });

        var listed = Assert.Single(listResponse.ReadAsJson<List<TaskTemplateDto>>(), t => t.Id == template.Id);
        Assert.True(listed.IsArchived);

        // Archived templates reject further subtask writes -- ResolveForManageAsync's
        // (template is null || template.IsArchived) check collapses this to NotFound, the same
        // treatment UpdateMealDetailsHandler gives an archived Meal.
        await TaskLibraryTestHelpers.AddSubtaskAsync(fixture, guardianToken, template.Id, expectedStatus: 404);
    }

    [Fact]
    public async Task The_child_cannot_archive_their_own_task_template()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var template = await TaskLibraryTestHelpers.CreateTaskTemplateAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(template);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Delete.Url($"/task-templates/{template.Id}");
            _.StatusCodeShouldBe(403);
        });
    }
}
