using buddy.Features.Groups;
using buddy.Features.Mealplans;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.Mealplans;
using buddy.IntegrationTests.Fixtures;

using Alba;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.SnapshotTests;

// Verifies the inline Marten snapshot (MealPlanSnapshotProjection, schema "snapshots") stays
// exactly consistent with a full replay-from-events rehydration after a sequence of commands --
// the invariant docs/backend/analysis/event-stream-snapshots.md relies on to say events remain
// the single source of truth and the snapshot is just a derived, quicker-to-read cache of the
// same state. Exercises the ImmutableDictionary<(DateOnly, MealSlot), ...> tuple-key path
// (ValueTupleJsonConverterFactory) via MealAssignedToSlot.
[Collection(BuddyApiCollection.Name)]
public sealed class MealPlanSnapshotTests(BuddyApiFixture fixture)
{
    [Fact]
    public async Task The_snapshot_matches_a_full_replay_after_several_commands()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var meal = await MealplanTestHelpers.CreateMealAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(meal);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // MealAssignedToSlot on a family with no plan stream yet lazily creates the plan, so this
        // single call exercises both MealPlanCreated and MealAssignedToSlot.
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { MealId = meal.Id, Notes = "No cilantro" })
                .ToUrl($"/mealplans/children/{child.Id}/plan")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "Dinner");
            _.StatusCodeShouldBeOk();
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { Times = new Dictionary<string, string> { ["Dinner"] = "19:30:00" } })
                .ToUrl($"/mealplans/children/{child.Id}/slot-times");
            _.StatusCodeShouldBe(204);
        });

        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, guardianToken, "Co-parents");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Url($"/mealplans/children/{child.Id}/plan/groups/{groupId}");
            _.StatusCodeShouldBe(204);
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Delete.Url($"/mealplans/children/{child.Id}/plan")
                .QueryString("date", $"{today:yyyy-MM-dd}")
                .QueryString("slot", "Dinner");
            _.StatusCodeShouldBe(204);
        });

        var mealPlans = fixture.Host.Services.GetRequiredService<IMealPlanEventStore>();
        var id = await mealPlans.FindIdForChildAsync(new UserId(child.Id), CancellationToken.None);
        Assert.NotNull(id);

        var events = await mealPlans.ReadAsync(id, CancellationToken.None);
        var replayed = MealPlan.Rehydrate(events);
        var snapshot = await mealPlans.FindSnapshotAsync(id, CancellationToken.None);

        Assert.NotNull(replayed);
        Assert.NotNull(snapshot);
        Assert.Equivalent(replayed, snapshot, strict: true);
    }
}
