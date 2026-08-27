using Alba;

using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.TaskLibrary;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.TaskLibrary.UpdateTaskTemplate;

[Collection(BuddyApiCollection.Name)]
public sealed class UpdateTaskTemplateTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("UpdateTaskTemplate")]
    public async Task A_guardian_can_update_a_task_templates_name_icon_and_color()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var template = await TaskLibraryTestHelpers.CreateTaskTemplateAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(template);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Patch.Json(new { Name = "Evening routine", Icon = "moon", Color = "#3355ff" })
                .ToUrl($"/task-templates/{template.Id}");
            _.StatusCodeShouldBeOk();
        });

        var updated = response.ReadAsJson<TaskTemplateDto>();
        Assert.Equal("Evening routine", updated.Name);
        Assert.Equal("moon", updated.Icon);
        Assert.Equal("#3355ff", updated.Color);
    }

    [Fact]
    public async Task An_archived_task_template_cannot_be_updated()
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

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Patch.Json(new { Name = "Evening routine", Icon = "moon", Color = "#3355ff" })
                .ToUrl($"/task-templates/{template.Id}");
            _.StatusCodeShouldBe(404);
        });
    }

    [Fact]
    public async Task The_child_cannot_update_a_task_template()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var template = await TaskLibraryTestHelpers.CreateTaskTemplateAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(template);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Patch.Json(new { Name = "Evening routine", Icon = "moon", Color = "#3355ff" })
                .ToUrl($"/task-templates/{template.Id}");
            _.StatusCodeShouldBe(403);
        });
    }
}
