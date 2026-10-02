using Alba;

using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.WorkLocations.AddWorkLocation;

[Collection(BuddyApiCollection.Name)]
public sealed class AddWorkLocationTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("AddWorkLocation")]
    public async Task A_guardian_can_add_a_custom_location_and_read_it_back()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();

        var added = await WorkLocationTestHelpers.AddLocationAsync(fixture, token, "  Randers  ", "🚆", "#dc2626");

        Assert.Equal("Randers", added.Name);
        Assert.Equal("🚆", added.Icon);
        Assert.Equal("#dc2626", added.Color);
        Assert.False(added.IsArchived);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"/work-locations/guardians/{guardianId}");
            _.StatusCodeShouldBeOk();
        });

        var location = Assert.Single(response.ReadAsJson<WorkLocationScheduleDto>().Locations);
        Assert.Equal(added.Id, location.Id);
    }

    [Theory]
    [InlineData("", "🏢", "#2563eb")]
    [InlineData("   ", "🏢", "#2563eb")]
    [InlineData("A name that is far too long to fit on a printed sheet", "🏢", "#2563eb")]
    [InlineData("Stil", "", "#2563eb")]
    [InlineData("Stil", "🏢", "")]
    public async Task Missing_or_oversized_details_are_rejected(string name, string icon, string color)
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(new { Name = name, Icon = icon, Color = color }).ToUrl("/work-locations/me/locations");
            _.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task A_name_already_used_by_an_active_location_is_rejected_case_insensitively()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        await WorkLocationTestHelpers.AddLocationAsync(fixture, token, "Stil");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(new { Name = "STIL", Icon = "🏢", Color = "#2563eb" }).ToUrl("/work-locations/me/locations");
            _.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task An_archived_locations_name_can_be_reused()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var first = await WorkLocationTestHelpers.AddLocationAsync(fixture, token, "Stil");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url($"/work-locations/me/locations/{first.Id}");
            _.StatusCodeShouldBe(204);
        });

        var second = await WorkLocationTestHelpers.AddLocationAsync(fixture, token, "Stil");

        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task A_thirteenth_active_location_is_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        for (var i = 1; i <= 12; i++)
        {
            await WorkLocationTestHelpers.AddLocationAsync(fixture, token, $"Place {i}");
        }

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(new { Name = "Place 13", Icon = "🏢", Color = "#2563eb" }).ToUrl("/work-locations/me/locations");
            _.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task A_child_account_is_forbidden()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Post.Json(new { Name = "School", Icon = "🏫", Color = "#2563eb" }).ToUrl("/work-locations/me/locations");
            _.StatusCodeShouldBe(403);
        });
    }
}
