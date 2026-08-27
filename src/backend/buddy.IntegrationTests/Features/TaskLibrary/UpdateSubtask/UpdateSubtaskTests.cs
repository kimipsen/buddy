using Alba;

using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.TaskLibrary;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.TaskLibrary.UpdateSubtask;

[Collection(BuddyApiCollection.Name)]
public sealed class UpdateSubtaskTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("UpdateSubtask")]
    public async Task A_guardian_can_update_a_subtask_in_place()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var template = await TaskLibraryTestHelpers.CreateTaskTemplateAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(template);
        var withSubtask = await TaskLibraryTestHelpers.AddSubtaskAsync(fixture, guardianToken, template.Id);
        Assert.NotNull(withSubtask);
        var subtaskId = Assert.Single(withSubtask.Subtasks).Id;

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Patch.Json(new { Title = "Brush teeth thoroughly", Icon = "toothbrush", Duration = "00:03:00" })
                .ToUrl($"/task-templates/{template.Id}/subtasks/{subtaskId}");
            _.StatusCodeShouldBeOk();
        });

        var updated = response.ReadAsJson<TaskTemplateDto>();
        var subtask = Assert.Single(updated.Subtasks);
        Assert.Equal(subtaskId, subtask.Id);
        Assert.Equal("Brush teeth thoroughly", subtask.Title);
        Assert.Equal(TimeSpan.FromMinutes(3), subtask.Duration);
    }

    [Fact]
    public async Task Updating_an_unknown_subtask_id_returns_not_found()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var template = await TaskLibraryTestHelpers.CreateTaskTemplateAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(template);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Patch.Json(new { Title = "Ghost subtask", Icon = (string?)null, Duration = "00:01:00" })
                .ToUrl($"/task-templates/{template.Id}/subtasks/{Guid.NewGuid()}");
            _.StatusCodeShouldBe(404);
        });
    }
}
