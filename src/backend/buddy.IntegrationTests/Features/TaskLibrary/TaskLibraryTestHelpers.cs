using Alba;

using buddy.IntegrationTests.Fixtures;

namespace buddy.IntegrationTests.Features.TaskLibrary;

internal sealed record CreateTaskTemplateOptions(string Name = "Morning routine", string Icon = "sunrise", string Color = "#ffaa00");

internal sealed record AddSubtaskOptions(string Title = "Brush teeth", string? Icon = "toothbrush", string Duration = "00:02:00", int? Position = null);

internal static class TaskLibraryTestHelpers
{
    public static async Task<TaskTemplateDto?> CreateTaskTemplateAsync(
        BuddyApiFixture fixture, string guardianToken, Guid childId, CreateTaskTemplateOptions? options = null, int expectedStatus = 200)
    {
        options ??= new CreateTaskTemplateOptions();

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new { options.Name, options.Icon, options.Color }).ToUrl($"/task-templates/children/{childId}");
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<TaskTemplateDto>() : null;
    }

    public static async Task<TaskTemplateDto?> AddSubtaskAsync(
        BuddyApiFixture fixture, string guardianToken, Guid templateId, AddSubtaskOptions? options = null, int expectedStatus = 200)
    {
        options ??= new AddSubtaskOptions();

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new { options.Title, options.Icon, Duration = options.Duration, options.Position }).ToUrl($"/task-templates/{templateId}/subtasks");
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<TaskTemplateDto>() : null;
    }
}
