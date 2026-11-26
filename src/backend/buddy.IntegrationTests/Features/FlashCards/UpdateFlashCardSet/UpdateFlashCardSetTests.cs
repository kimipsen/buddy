using buddy.Features.FlashCards;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.FlashCards.FlashCardTestHelpers;

namespace buddy.IntegrationTests.Features.FlashCards.UpdateFlashCardSet;

[Collection(BuddyApiCollection.Name)]
public sealed class UpdateFlashCardSetTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("UpdateFlashCardSet")]
    public async Task A_guardian_can_rename_a_set()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var set = await CreateTextSetAsync(fixture, token, "Old");

        var updated = (await PutAsync(fixture, token, $"/flash-cards/sets/{set.Id}", new { Name = " New " })).ReadAsJson<FlashCardSetResponse>();

        Assert.Equal("New", updated.Name);
        Assert.Equal("New", (await GetAsync(fixture, token, "/flash-cards/sets")).ReadAsJson<List<FlashCardSetResponse>>().Single(s => s.Id == set.Id).Name);
    }

    [Fact]
    public async Task A_guardian_can_change_a_math_sets_rule_and_a_rename_keeps_it()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var set = await CreateMathSetAsync(fixture, token);

        var renamed = (await PutAsync(fixture, token, $"/flash-cards/sets/{set.Id}", new { Name = "Renamed" })).ReadAsJson<FlashCardSetResponse>();
        Assert.Equal(set.Math, renamed.Math);

        var changed = (await PutAsync(fixture, token, $"/flash-cards/sets/{set.Id}", new { Name = "Renamed", Math = new { Operation = "Subtraction", Limit = 20 } }))
            .ReadAsJson<FlashCardSetResponse>();
        Assert.Equal(new MathRule(MathOperation.Subtraction, 20), changed.Math);
    }

    [Fact]
    public async Task Repeating_the_same_update_records_nothing()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var set = await CreateTextSetAsync(fixture, token, "Same");
        var store = fixture.Host.Services.GetRequiredService<IFlashCardSetEventStore>();

        await PutAsync(fixture, token, $"/flash-cards/sets/{set.Id}", new { Name = "Same" });
        await PutAsync(fixture, token, $"/flash-cards/sets/{set.Id}", new { Name = "Other" });
        await PutAsync(fixture, token, $"/flash-cards/sets/{set.Id}", new { Name = "Other" });

        Assert.Equal(2, (await store.ReadAsync(new FlashCardSetId(set.Id), CancellationToken.None)).Count);
    }

    [Fact]
    public async Task A_math_rule_on_a_text_set_is_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var set = await CreateTextSetAsync(fixture, token);

        await PutAsync(fixture, token, $"/flash-cards/sets/{set.Id}", new { Name = "X", Math = new { Operation = "Addition", Limit = 10 } }, 400);
    }

    [Theory]
    [InlineData("")]
    public async Task An_empty_name_is_rejected(string name)
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var set = await CreateTextSetAsync(fixture, token);

        await PutAsync(fixture, token, $"/flash-cards/sets/{set.Id}", new { Name = name }, 400);
    }

    [Fact]
    public async Task An_invalid_math_rule_is_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var set = await CreateMathSetAsync(fixture, token);

        await PutAsync(fixture, token, $"/flash-cards/sets/{set.Id}", new { Name = "X", Math = new { Operation = "Addition", Limit = 500 } }, 400);
    }

    [Fact]
    public async Task An_unknown_or_deleted_set_is_not_found()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var set = await CreateTextSetAsync(fixture, token);
        await DeleteAsync(fixture, token, $"/flash-cards/sets/{set.Id}");

        await PutAsync(fixture, token, $"/flash-cards/sets/{Guid.CreateVersion7()}", new { Name = "X" }, 404);
        await PutAsync(fixture, token, $"/flash-cards/sets/{set.Id}", new { Name = "X" }, 404);
    }
}
