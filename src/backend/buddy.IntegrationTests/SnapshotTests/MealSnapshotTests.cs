using buddy.Features.Mealplans;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.Mealplans;
using buddy.IntegrationTests.Fixtures;

using Alba;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.SnapshotTests;

// Verifies the inline Marten snapshot (MealSnapshotProjection, schema "snapshots") stays exactly
// consistent with a full replay-from-events rehydration after a sequence of commands -- the
// invariant docs/backend/analysis/event-stream-snapshots.md relies on to say events remain the
// single source of truth and the snapshot is just a derived, quicker-to-read cache of the same
// state.
[Collection(BuddyApiCollection.Name)]
public sealed class MealSnapshotTests(BuddyApiFixture fixture)
{
    [Fact]
    public async Task The_snapshot_matches_a_full_replay_after_several_commands()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(meal);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Patch.Json(new { Name = "Tacos (renamed)", Description = "New recipe", Icon = "burrito", Color = "#00aaff" })
                .ToUrl($"/mealplans/children/{child.Id}/meals/{meal.Id}/details");
            _.StatusCodeShouldBeOk();
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Put.Json(new { Stars = 5, Comment = "Loved it!" }).ToUrl($"/mealplans/children/{child.Id}/meals/{meal.Id}/rating");
            _.StatusCodeShouldBeOk();
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Delete.Url($"/mealplans/children/{child.Id}/meals/{meal.Id}");
            _.StatusCodeShouldBe(204);
        });

        var meals = fixture.Host.Services.GetRequiredService<IMealEventStore>();
        var id = new MealId(meal.Id);

        var events = await meals.ReadAsync(id, CancellationToken.None);
        var replayed = Meal.Rehydrate(events);
        var snapshot = await meals.FindSnapshotAsync(id, CancellationToken.None);

        Assert.NotNull(replayed);
        Assert.NotNull(snapshot);
        Assert.Equivalent(replayed, snapshot, strict: true);
    }
}
