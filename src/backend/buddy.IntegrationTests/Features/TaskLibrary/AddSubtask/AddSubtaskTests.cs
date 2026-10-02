using Alba;

using buddy.Common;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.TaskLibrary;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.TaskLibrary.AddSubtask;

[Collection(BuddyApiCollection.Name)]
public sealed class AddSubtaskTests(BuddyApiFixture fixture)
{
    private async Task<(string GuardianToken, Guid TemplateId)> CreateTemplateAsync()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var template = await TaskLibraryTestHelpers.CreateTaskTemplateAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(template);

        return (guardianToken, template.Id);
    }

    private async Task<IScenarioResult> PostRejectedSubtaskAsync(string guardianToken, Guid templateId, string title, string duration) =>
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new { Title = title, Icon = (string?)null, Duration = duration, Position = (int?)null })
                .ToUrl($"/task-templates/{templateId}/subtasks");
            _.StatusCodeShouldBe(400);
        });

    private static void AssertValidationErrorOn(IScenarioResult response, string field)
    {
        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Contains(field, error.Details.Keys);
    }

    [Fact]
    [CoversEndpoint("AddSubtask")]
    public async Task A_guardian_can_add_subtasks_which_accumulate_total_duration()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var template = await TaskLibraryTestHelpers.CreateTaskTemplateAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(template);

        var afterFirst = await TaskLibraryTestHelpers.AddSubtaskAsync(
            fixture, guardianToken, template.Id, new AddSubtaskOptions(Title: "Brush teeth", Duration: "00:02:00"));
        var afterSecond = await TaskLibraryTestHelpers.AddSubtaskAsync(
            fixture, guardianToken, template.Id, new AddSubtaskOptions(Title: "Get dressed", Duration: "00:05:00"));

        Assert.NotNull(afterFirst);
        Assert.NotNull(afterSecond);
        Assert.Equal(2, afterSecond.Subtasks.Count);
        Assert.Equal(["Brush teeth", "Get dressed"], afterSecond.Subtasks.Select(s => s.Title));
        Assert.Equal(TimeSpan.FromMinutes(7), afterSecond.TotalDuration);
    }

    [Fact]
    public async Task A_subtask_with_a_blank_icon_inherits_the_templates_icon()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var template = await TaskLibraryTestHelpers.CreateTaskTemplateAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(template);

        var updated = await TaskLibraryTestHelpers.AddSubtaskAsync(
            fixture, guardianToken, template.Id, new AddSubtaskOptions(Icon: " "));

        Assert.NotNull(updated);
        Assert.Null(Assert.Single(updated.Subtasks).Icon);
    }

    [Fact]
    public async Task A_subtask_with_zero_duration_is_rejected()
    {
        var (guardianToken, templateId) = await CreateTemplateAsync();

        var response = await PostRejectedSubtaskAsync(guardianToken, templateId, "Brush teeth", "00:00:00");

        AssertValidationErrorOn(response, "Duration");
    }

    [Fact]
    public async Task A_subtask_with_a_one_tick_duration_is_accepted()
    {
        var (guardianToken, templateId) = await CreateTemplateAsync();

        var updated = await TaskLibraryTestHelpers.AddSubtaskAsync(
            fixture, guardianToken, templateId, new AddSubtaskOptions(Duration: "00:00:00.0000001"));

        Assert.NotNull(updated);
        Assert.Equal(TimeSpan.FromTicks(1), Assert.Single(updated.Subtasks).Duration);
    }

    [Fact]
    public async Task A_subtask_with_a_blank_title_is_rejected()
    {
        var (guardianToken, templateId) = await CreateTemplateAsync();

        var response = await PostRejectedSubtaskAsync(guardianToken, templateId, " ", "00:02:00");

        AssertValidationErrorOn(response, "Title");
    }

    [Fact]
    public async Task A_subtask_title_of_200_characters_is_accepted()
    {
        var (guardianToken, templateId) = await CreateTemplateAsync();
        var title = new string('a', 200);

        var updated = await TaskLibraryTestHelpers.AddSubtaskAsync(fixture, guardianToken, templateId, new AddSubtaskOptions(Title: title));

        Assert.NotNull(updated);
        Assert.Equal(title, Assert.Single(updated.Subtasks).Title);
    }

    [Fact]
    public async Task A_subtask_title_of_201_characters_is_rejected()
    {
        var (guardianToken, templateId) = await CreateTemplateAsync();

        var response = await PostRejectedSubtaskAsync(guardianToken, templateId, new string('a', 201), "00:02:00");

        AssertValidationErrorOn(response, "Title");
    }

    [Fact]
    public async Task The_child_cannot_add_a_subtask()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var template = await TaskLibraryTestHelpers.CreateTaskTemplateAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(template);

        await TaskLibraryTestHelpers.AddSubtaskAsync(fixture, childToken, template.Id, expectedStatus: 403);
    }
}
