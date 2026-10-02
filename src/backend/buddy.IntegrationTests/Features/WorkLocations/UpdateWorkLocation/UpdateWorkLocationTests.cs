using Alba;

using buddy.Features.Users;
using buddy.Features.WorkLocations;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.Features.WorkLocations.UpdateWorkLocation;

[Collection(BuddyApiCollection.Name)]
public sealed class UpdateWorkLocationTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("UpdateWorkLocation")]
    public async Task A_guardian_can_rename_and_restyle_a_location()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var location = await WorkLocationTestHelpers.AddLocationAsync(fixture, token, "Stil");

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Name = "Kontoret", Icon = "🏬", Color = "#16a34a" }).ToUrl($"/work-locations/me/locations/{location.Id}");
            _.StatusCodeShouldBeOk();
        });

        var updated = response.ReadAsJson<WorkLocationDto>();
        Assert.Equal(new WorkLocationDto(location.Id, "Kontoret", "🏬", "#16a34a", false), updated);
    }

    [Fact]
    public async Task Saving_unchanged_details_appends_no_event()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var location = await WorkLocationTestHelpers.AddLocationAsync(fixture, token, "Stil", "🏢", "#2563eb");
        var store = fixture.Host.Services.GetRequiredService<IWorkLocationScheduleEventStore>();
        var id = WorkLocationScheduleId.ForGuardian(new UserId(guardianId));
        var before = (await store.ReadAsync(id, CancellationToken.None)).Count;

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Name = "Stil", Icon = "🏢", Color = "#2563eb" }).ToUrl($"/work-locations/me/locations/{location.Id}");
            _.StatusCodeShouldBeOk();
        });

        Assert.Equal(before, (await store.ReadAsync(id, CancellationToken.None)).Count);
    }

    [Fact]
    public async Task Renaming_to_another_active_locations_name_is_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        await WorkLocationTestHelpers.AddLocationAsync(fixture, token, "Stil");
        var randers = await WorkLocationTestHelpers.AddLocationAsync(fixture, token, "Randers");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Name = "stil", Icon = "🏢", Color = "#2563eb" }).ToUrl($"/work-locations/me/locations/{randers.Id}");
            _.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task Blank_details_are_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var location = await WorkLocationTestHelpers.AddLocationAsync(fixture, token, "Stil");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Name = "", Icon = "🏢", Color = "#2563eb" }).ToUrl($"/work-locations/me/locations/{location.Id}");
            _.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task An_unknown_or_archived_location_is_not_found()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var location = await WorkLocationTestHelpers.AddLocationAsync(fixture, token, "Stil");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Name = "Elsewhere", Icon = "🏢", Color = "#2563eb" }).ToUrl($"/work-locations/me/locations/{Guid.NewGuid()}");
            _.StatusCodeShouldBe(404);
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url($"/work-locations/me/locations/{location.Id}");
            _.StatusCodeShouldBe(204);
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Name = "Elsewhere", Icon = "🏢", Color = "#2563eb" }).ToUrl($"/work-locations/me/locations/{location.Id}");
            _.StatusCodeShouldBe(404);
        });
    }

    [Fact]
    public async Task Another_guardians_location_is_not_found()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var location = await WorkLocationTestHelpers.AddLocationAsync(fixture, ownerToken, "Stil");
        var (_, otherToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {otherToken}");
            _.Patch.Json(new { Name = "Mine now", Icon = "🏢", Color = "#2563eb" }).ToUrl($"/work-locations/me/locations/{location.Id}");
            _.StatusCodeShouldBe(404);
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
            _.Patch.Json(new { Name = "School", Icon = "🏫", Color = "#2563eb" }).ToUrl($"/work-locations/me/locations/{Guid.NewGuid()}");
            _.StatusCodeShouldBe(403);
        });
    }
}
