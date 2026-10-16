using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;

using Xunit;

namespace buddy.IntegrationTests.Features.TaskLibrary;

// Templates and routine steps may carry a pictogram ("pictogram:<name>") instead of an emoji
// (docs/frontend/analysis/pictograms.md, Question 2).
[Collection(BuddyApiCollection.Name)]
public sealed class PictogramIconTests(BuddyApiFixture fixture)
{
    private const string Malformed = "pictogram:Brush Teeth";

    [Fact]
    public async Task A_template_and_its_steps_can_carry_pictograms()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");

        var template = await TaskLibraryTestHelpers.CreateTaskTemplateAsync(
            fixture, token, child.Id, new CreateTaskTemplateOptions(Icon: "pictogram:wake-up"));
        Assert.NotNull(template);
        Assert.Equal("pictogram:wake-up", template.Icon);

        var withStep = await TaskLibraryTestHelpers.AddSubtaskAsync(
            fixture, token, template.Id, new AddSubtaskOptions("Brush teeth", "pictogram:brush-teeth"));
        var step = Assert.Single(withStep!.Subtasks);
        Assert.Equal("pictogram:brush-teeth", step.Icon);

        var updatedStep = await PatchAsync(
            token, $"/task-templates/{template.Id}/subtasks/{step.Id}",
            new { Title = "Wash", Duration = "00:02:00", Icon = "pictogram:wash-face-hands" });
        Assert.Equal("pictogram:wash-face-hands", Assert.Single(updatedStep!.Subtasks).Icon);

        var renamed = await PatchAsync(
            token, $"/task-templates/{template.Id}", new { Name = "Mornings", Icon = "pictogram:get-dressed", Color = "#ffaa00" });
        Assert.Equal("pictogram:get-dressed", renamed!.Icon);
    }

    [Fact]
    public async Task A_malformed_pictogram_is_rejected_on_every_template_and_step_write()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");

        await TaskLibraryTestHelpers.CreateTaskTemplateAsync(
            fixture, token, child.Id, new CreateTaskTemplateOptions(Icon: Malformed), expectedStatus: 400);

        var template = await TaskLibraryTestHelpers.CreateTaskTemplateAsync(fixture, token, child.Id);
        Assert.NotNull(template);

        await TaskLibraryTestHelpers.AddSubtaskAsync(
            fixture, token, template.Id, new AddSubtaskOptions(Icon: Malformed), expectedStatus: 400);

        var withStep = await TaskLibraryTestHelpers.AddSubtaskAsync(fixture, token, template.Id);
        var step = Assert.Single(withStep!.Subtasks);

        await PatchAsync(
            token, $"/task-templates/{template.Id}/subtasks/{step.Id}",
            new { Title = "Brush teeth", Duration = "00:02:00", Icon = Malformed }, expectedStatus: 400);
        await PatchAsync(
            token, $"/task-templates/{template.Id}", new { Name = "Mornings", Icon = Malformed, Color = "#ffaa00" }, expectedStatus: 400);
    }

    private async Task<TaskTemplateDto?> PatchAsync(string token, string url, object body, int expectedStatus = 200)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(body).ToUrl(url);
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<TaskTemplateDto>() : null;
    }
}
