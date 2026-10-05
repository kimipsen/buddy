using Alba;

using buddy.Features.Babysitters;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.Features.Babysitters.UpdateBabysitter;

[Collection(BuddyApiCollection.Name)]
public sealed class UpdateBabysitterTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("UpdateBabysitter")]
    public async Task A_guardian_can_rename_a_babysitter_and_change_contact_info()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var anna = await BabysitterTestHelpers.AddAsync(fixture, token, "Anna");

        var updated = (await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Name = "Anne", ContactInfo = "anne@example.com" }).ToUrl($"/babysitters/me/{anna.Id}");
            _.StatusCodeShouldBeOk();
        })).ReadAsJson<BabysitterDto>();

        Assert.Equal(new BabysitterDto(anna.Id, "Anne", "anne@example.com", false), updated);
        Assert.Equal(updated, Assert.Single(await BabysitterTestHelpers.ListMineAsync(fixture, token)));
    }

    [Fact]
    public async Task Saving_unchanged_details_appends_no_event()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var anna = await BabysitterTestHelpers.AddAsync(fixture, token, "Anna", "123");
        var store = fixture.Host.Services.GetRequiredService<IBabysitterListEventStore>();
        var id = BabysitterListId.ForGuardian(new UserId(guardianId));
        var before = (await store.ReadAsync(id, CancellationToken.None)).Count;

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Name = " Anna ", ContactInfo = "123" }).ToUrl($"/babysitters/me/{anna.Id}");
            _.StatusCodeShouldBeOk();
        });

        Assert.Equal(before, (await store.ReadAsync(id, CancellationToken.None)).Count);
    }

    [Fact]
    public async Task Renaming_to_another_active_babysitters_name_is_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        await BabysitterTestHelpers.AddAsync(fixture, token, "Anna");
        var bo = await BabysitterTestHelpers.AddAsync(fixture, token, "Bo");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Name = "anna" }).ToUrl($"/babysitters/me/{bo.Id}");
            _.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task An_empty_name_is_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var anna = await BabysitterTestHelpers.AddAsync(fixture, token, "Anna");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Name = " " }).ToUrl($"/babysitters/me/{anna.Id}");
            _.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task An_archived_or_unknown_babysitter_is_not_found()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var anna = await BabysitterTestHelpers.AddAsync(fixture, token, "Anna");
        await BabysitterTestHelpers.ArchiveAsync(fixture, token, anna.Id);

        foreach (var id in new[] { anna.Id, Guid.CreateVersion7() })
        {
            await fixture.Host.Scenario(_ =>
            {
                _.WithRequestHeader("Authorization", $"Bearer {token}");
                _.Patch.Json(new { Name = "Anne" }).ToUrl($"/babysitters/me/{id}");
                _.StatusCodeShouldBe(404);
            });
        }
    }

    [Fact]
    public async Task Another_guardians_babysitter_is_not_found()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var anna = await BabysitterTestHelpers.AddAsync(fixture, ownerToken, "Anna");
        var (_, otherToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {otherToken}");
            _.Patch.Json(new { Name = "Anne" }).ToUrl($"/babysitters/me/{anna.Id}");
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
            _.Patch.Json(new { Name = "Anna" }).ToUrl($"/babysitters/me/{Guid.CreateVersion7()}");
            _.StatusCodeShouldBe(403);
        });
    }
}
