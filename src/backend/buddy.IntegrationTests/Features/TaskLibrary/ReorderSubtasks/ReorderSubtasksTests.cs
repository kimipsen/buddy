using Alba;

using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.TaskLibrary;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.TaskLibrary.ReorderSubtasks;

[Collection(BuddyApiCollection.Name)]
public sealed class ReorderSubtasksTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ReorderSubtasks")]
    public async Task A_guardian_can_reorder_subtasks()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var template = await TaskLibraryTestHelpers.CreateTaskTemplateAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(template);
        await TaskLibraryTestHelpers.AddSubtaskAsync(fixture, guardianToken, template.Id, new AddSubtaskOptions(Title: "Brush teeth"));
        var withBoth = await TaskLibraryTestHelpers.AddSubtaskAsync(fixture, guardianToken, template.Id, new AddSubtaskOptions(Title: "Get dressed"));
        Assert.NotNull(withBoth);
        Assert.Equal(["Brush teeth", "Get dressed"], withBoth.Subtasks.Select(s => s.Title));

        var reversedIds = withBoth.Subtasks.Select(s => s.Id).Reverse().ToArray();

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { NewOrder = reversedIds }).ToUrl($"/task-templates/{template.Id}/subtasks/order");
            _.StatusCodeShouldBeOk();
        });

        var reordered = response.ReadAsJson<TaskTemplateDto>();
        Assert.Equal(["Get dressed", "Brush teeth"], reordered.Subtasks.Select(s => s.Title));
    }

    [Fact]
    public async Task A_new_order_that_is_not_a_permutation_of_current_subtasks_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var template = await TaskLibraryTestHelpers.CreateTaskTemplateAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(template);
        await TaskLibraryTestHelpers.AddSubtaskAsync(fixture, guardianToken, template.Id, new AddSubtaskOptions(Title: "Brush teeth"));

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { NewOrder = new[] { Guid.NewGuid() } }).ToUrl($"/task-templates/{template.Id}/subtasks/order");
            _.StatusCodeShouldBe(400);
        });
    }
}
