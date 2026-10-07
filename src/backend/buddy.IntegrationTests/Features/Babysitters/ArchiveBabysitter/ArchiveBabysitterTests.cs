using buddy.Features.Babysitters;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.Features.Babysitters.ArchiveBabysitter;

[Collection(BuddyApiCollection.Name)]
public sealed class ArchiveBabysitterTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ArchiveBabysitter")]
    public async Task Archiving_keeps_the_babysitter_in_the_list_flagged_as_archived()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var anna = await BabysitterTestHelpers.AddAsync(fixture, token, "Anna");

        await BabysitterTestHelpers.ArchiveAsync(fixture, token, anna.Id);

        Assert.True(Assert.Single(await BabysitterTestHelpers.ListMineAsync(fixture, token)).IsArchived);
    }

    [Fact]
    public async Task Archiving_twice_is_idempotent_and_appends_one_event()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var anna = await BabysitterTestHelpers.AddAsync(fixture, token, "Anna");

        await BabysitterTestHelpers.ArchiveAsync(fixture, token, anna.Id);
        await BabysitterTestHelpers.ArchiveAsync(fixture, token, anna.Id);

        var store = fixture.Host.Services.GetRequiredService<IBabysitterListEventStore>();
        var events = await store.ReadAsync(BabysitterListId.ForGuardian(new UserId(guardianId)), CancellationToken.None);
        Assert.Single(events, e => e.Value is BabysitterArchived);
    }

    [Fact]
    public async Task An_unknown_babysitter_is_not_found()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url($"/babysitters/me/{Guid.CreateVersion7()}");
            _.StatusCodeShouldBe(404);
        });
    }

    [Fact]
    public async Task Another_guardians_babysitter_is_not_found_and_stays_active()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var anna = await BabysitterTestHelpers.AddAsync(fixture, ownerToken, "Anna");
        var (_, otherToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {otherToken}");
            _.Delete.Url($"/babysitters/me/{anna.Id}");
            _.StatusCodeShouldBe(404);
        });

        Assert.False(Assert.Single(await BabysitterTestHelpers.ListMineAsync(fixture, ownerToken)).IsArchived);
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
            _.Delete.Url($"/babysitters/me/{Guid.CreateVersion7()}");
            _.StatusCodeShouldBe(403);
        });
    }
}
