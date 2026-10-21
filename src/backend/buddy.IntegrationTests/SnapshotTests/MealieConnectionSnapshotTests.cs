using buddy.Features.Mealplans;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Mealplans;
using buddy.IntegrationTests.Features.Mealplans.Mealie;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.SnapshotTests;

// The inline MealieConnectionSnapshotProjection (and MealSnapshotProjection's two recipe-link
// events) stay equal to a full replay -- see docs/backend/analysis/event-stream-snapshots.md.
[Collection(BuddyApiCollection.Name)]
public sealed class MealieConnectionSnapshotTests(BuddyApiFixture fixture)
{
    [Fact]
    public async Task The_connection_snapshot_matches_a_full_replay_after_connect_reconnect_and_disconnect()
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture);
        family.Server.ValidTokens.Add("rotated-token-5678");
        await family.ConnectAsync(apiToken: "rotated-token-5678");
        await family.DeleteAsync("mealie");
        await family.ConnectAsync();

        var connections = family.Host.Services.GetRequiredService<IMealieConnectionEventStore>();
        var id = Assert.Single(await connections.ListIdsForChildAsync(new UserId(family.ChildId), CancellationToken.None));

        var events = await connections.ReadAsync(id, CancellationToken.None);
        var replayed = MealieConnection.Rehydrate(events);
        var snapshot = await connections.FindSnapshotAsync(id, CancellationToken.None);

        Assert.Equal(5, events.Count);
        Assert.NotNull(snapshot);
        Assert.Equivalent(replayed, snapshot, strict: true);
        Assert.Equal("1234", snapshot.Current?.Token.Last4);
    }

    [Fact]
    public async Task The_meal_snapshot_matches_a_full_replay_after_linking_and_unlinking()
    {
        var family = await MealieTestHost.CreateFamilyAsync(fixture);
        var first = family.Server.AddRecipe("Lasagne al forno");
        var second = family.Server.AddRecipe("Lasagne verde");
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, family.Token, family.ChildId);
        await family.PutAsync($"meals/{meal!.Id}/recipe", new { RecipeId = first.Id });
        await family.PutAsync($"meals/{meal.Id}/recipe", new { RecipeId = second.Id });
        await family.DeleteAsync($"meals/{meal.Id}/recipe");
        await family.PutAsync($"meals/{meal.Id}/recipe", new { RecipeId = first.Id });

        var meals = family.Host.Services.GetRequiredService<IMealEventStore>();
        var replayed = Meal.Rehydrate(await meals.ReadAsync(new MealId(meal.Id), CancellationToken.None));
        var snapshot = await meals.FindSnapshotAsync(new MealId(meal.Id), CancellationToken.None);

        Assert.NotNull(snapshot);
        Assert.Equivalent(replayed, snapshot, strict: true);
        Assert.Equal(first.Id, snapshot.Recipe?.RecipeId);
    }
}
