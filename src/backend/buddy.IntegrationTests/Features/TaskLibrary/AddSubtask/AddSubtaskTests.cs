using Alba;

using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.TaskLibrary;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.TaskLibrary.AddSubtask;

[Collection(BuddyApiCollection.Name)]
public sealed class AddSubtaskTests(BuddyApiFixture fixture)
{
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
    public async Task A_subtask_with_zero_duration_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var template = await TaskLibraryTestHelpers.CreateTaskTemplateAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(template);

        await TaskLibraryTestHelpers.AddSubtaskAsync(
            fixture, guardianToken, template.Id, new AddSubtaskOptions(Duration: "00:00:00"), expectedStatus: 400);
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
