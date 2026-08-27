using Alba;

using buddy.Common;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.TaskLibrary;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.TaskLibrary.CreateTaskTemplate;

[Collection(BuddyApiCollection.Name)]
public sealed class CreateTaskTemplateTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("CreateTaskTemplate")]
    public async Task A_guardian_can_create_a_task_template_for_their_child()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        var template = await TaskLibraryTestHelpers.CreateTaskTemplateAsync(fixture, guardianToken, child.Id);

        Assert.NotNull(template);
        Assert.Equal("Morning routine", template.Name);
        Assert.False(template.IsArchived);
        Assert.Empty(template.Subtasks);
        Assert.Equal(TimeSpan.Zero, template.TotalDuration);
    }

    [Fact]
    public async Task A_task_template_with_no_name_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new { Name = " ", Icon = "sunrise", Color = "#ffaa00" })
                .ToUrl($"/task-templates/children/{child.Id}");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Contains("Name", error.Details.Keys);
    }

    [Fact]
    public async Task The_child_cannot_create_their_own_task_template()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        await TaskLibraryTestHelpers.CreateTaskTemplateAsync(fixture, childToken, child.Id, expectedStatus: 403);
    }

    [Fact]
    public async Task A_third_party_with_no_guardian_link_gets_not_found()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await TaskLibraryTestHelpers.CreateTaskTemplateAsync(fixture, strangerToken, child.Id, expectedStatus: 404);
    }
}
