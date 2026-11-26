using buddy.Features.FlashCards;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.FlashCards.FlashCardTestHelpers;

namespace buddy.IntegrationTests.Features.FlashCards.DeleteFlashCardSet;

[Collection(BuddyApiCollection.Name)]
public sealed class DeleteFlashCardSetTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("DeleteFlashCardSet")]
    public async Task A_guardian_can_delete_a_set_softly()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var set = await CreateTextSetAsync(fixture, token);
        var store = fixture.Host.Services.GetRequiredService<IFlashCardSetEventStore>();

        await DeleteAsync(fixture, token, $"/flash-cards/sets/{set.Id}");

        var snapshot = await store.FindSnapshotAsync(new FlashCardSetId(set.Id), CancellationToken.None);
        Assert.True(snapshot!.IsDeleted);
        Assert.NotNull(snapshot.DeletedAt);
    }

    [Fact]
    public async Task Deleting_twice_succeeds_and_records_one_deletion()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var set = await CreateTextSetAsync(fixture, token);
        var store = fixture.Host.Services.GetRequiredService<IFlashCardSetEventStore>();

        await DeleteAsync(fixture, token, $"/flash-cards/sets/{set.Id}");
        await DeleteAsync(fixture, token, $"/flash-cards/sets/{set.Id}");

        Assert.Equal(2, (await store.ReadAsync(new FlashCardSetId(set.Id), CancellationToken.None)).Count);
    }

    [Fact]
    public async Task Deleting_an_unknown_set_is_not_found()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        await DeleteAsync(fixture, token, $"/flash-cards/sets/{Guid.CreateVersion7()}", 404);
    }
}
