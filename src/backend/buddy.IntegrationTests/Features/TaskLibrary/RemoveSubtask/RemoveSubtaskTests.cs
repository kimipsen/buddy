using Alba;

using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.TaskLibrary;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.TaskLibrary.RemoveSubtask;

[Collection(BuddyApiCollection.Name)]
public sealed class RemoveSubtaskTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("RemoveSubtask")]
    public async Task A_guardian_can_remove_a_subtask()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var template = await TaskLibraryTestHelpers.CreateTaskTemplateAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(template);
        var withSubtask = await TaskLibraryTestHelpers.AddSubtaskAsync(fixture, guardianToken, template.Id);
        Assert.NotNull(withSubtask);
        var subtaskId = Assert.Single(withSubtask.Subtasks).Id;

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Delete.Url($"/task-templates/{template.Id}/subtasks/{subtaskId}");
            _.StatusCodeShouldBe(204);
        });

        var listResponse = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/task-templates/children/{child.Id}");
            _.StatusCodeShouldBeOk();
        });

        var listed = Assert.Single(listResponse.ReadAsJson<List<TaskTemplateDto>>(), t => t.Id == template.Id);
        Assert.Empty(listed.Subtasks);
    }

    [Fact]
    public async Task Removing_an_unknown_subtask_id_returns_not_found()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var template = await TaskLibraryTestHelpers.CreateTaskTemplateAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(template);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Delete.Url($"/task-templates/{template.Id}/subtasks/{Guid.NewGuid()}");
            _.StatusCodeShouldBe(404);
        });
    }
}
