using Alba;

using buddy.Common;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.TaskLibrary;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.TaskLibrary.UpdateSubtask;

[Collection(BuddyApiCollection.Name)]
public sealed class UpdateSubtaskTests(BuddyApiFixture fixture)
{
    private async Task<(string GuardianToken, Guid TemplateId, Guid SubtaskId)> CreateTemplateWithSubtaskAsync()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var template = await TaskLibraryTestHelpers.CreateTaskTemplateAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(template);
        var withSubtask = await TaskLibraryTestHelpers.AddSubtaskAsync(fixture, guardianToken, template.Id);
        Assert.NotNull(withSubtask);

        return (guardianToken, template.Id, Assert.Single(withSubtask.Subtasks).Id);
    }

    private async Task<IScenarioResult> PatchSubtaskAsync(
        string guardianToken, Guid templateId, Guid subtaskId, string title, string duration, int expectedStatus) =>
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Patch.Json(new { Title = title, Icon = (string?)null, Duration = duration })
                .ToUrl($"/task-templates/{templateId}/subtasks/{subtaskId}");
            _.StatusCodeShouldBe(expectedStatus);
        });

    private static void AssertValidationErrorOn(IScenarioResult response, string field)
    {
        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Contains(field, error.Details.Keys);
    }

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

    [Fact]
    public async Task A_subtask_cannot_be_updated_to_a_blank_title()
    {
        var (guardianToken, templateId, subtaskId) = await CreateTemplateWithSubtaskAsync();

        var response = await PatchSubtaskAsync(guardianToken, templateId, subtaskId, " ", "00:02:00", expectedStatus: 400);

        AssertValidationErrorOn(response, "Title");
    }

    [Fact]
    public async Task A_subtask_title_of_200_characters_is_accepted()
    {
        var (guardianToken, templateId, subtaskId) = await CreateTemplateWithSubtaskAsync();
        var title = new string('a', 200);

        var response = await PatchSubtaskAsync(guardianToken, templateId, subtaskId, title, "00:02:00", expectedStatus: 200);

        Assert.Equal(title, Assert.Single(response.ReadAsJson<TaskTemplateDto>().Subtasks).Title);
    }

    [Fact]
    public async Task A_subtask_title_of_201_characters_is_rejected()
    {
        var (guardianToken, templateId, subtaskId) = await CreateTemplateWithSubtaskAsync();

        var response = await PatchSubtaskAsync(guardianToken, templateId, subtaskId, new string('a', 201), "00:02:00", expectedStatus: 400);

        AssertValidationErrorOn(response, "Title");
    }

    [Fact]
    public async Task A_subtask_cannot_be_updated_to_zero_duration()
    {
        var (guardianToken, templateId, subtaskId) = await CreateTemplateWithSubtaskAsync();

        var response = await PatchSubtaskAsync(guardianToken, templateId, subtaskId, "Brush teeth", "00:00:00", expectedStatus: 400);

        AssertValidationErrorOn(response, "Duration");
    }

    [Fact]
    public async Task A_subtask_can_be_updated_to_a_one_tick_duration()
    {
        var (guardianToken, templateId, subtaskId) = await CreateTemplateWithSubtaskAsync();

        var response = await PatchSubtaskAsync(guardianToken, templateId, subtaskId, "Brush teeth", "00:00:00.0000001", expectedStatus: 200);

        Assert.Equal(TimeSpan.FromTicks(1), Assert.Single(response.ReadAsJson<TaskTemplateDto>().Subtasks).Duration);
    }
}
