using buddy.Features.Mealplans;
using buddy.Features.Users;

using Xunit;

namespace buddy.IntegrationTests.EventShapeTests;

// The Mealie integration's events (docs/backend/analysis/mealie-integration.md): the
// MealieConnection aggregate and the two recipe-link events on Meal. All in the Mealplans store.
public sealed class MealplanMealieEventShapeTests
{
    private static readonly MealieConnectionId FixedConnectionId = new(Guid.Parse("00000000-0000-0000-0000-000000000080"));
    private static readonly MealId FixedMealId = new(Guid.Parse("00000000-0000-0000-0000-000000000060"));
    private static readonly UserId FixedChildId = new(Guid.Parse("00000000-0000-0000-0000-000000000003"));
    private static readonly UserId FixedGuardianId = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    private static readonly DateTimeOffset FixedInstant = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly MealieRecipeRef FixedRecipe = new(Guid.Parse("00000000-0000-0000-0000-000000000081"), "lasagne-al-forno", "Lasagne al forno");

    [Fact]
    public void MealieConnectionInitialized() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new MealieConnectionInitialized(FixedConnectionId, FixedChildId, FixedInstant),
        "Mealplans/MealieConnectionInitialized.json");

    [Fact]
    public void MealieConnected() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new MealieConnected(
            FixedConnectionId,
            "https://mealie.example.com",
            new StoredApiKey("CfDJ8-ciphertext", "b7f2", FixedGuardianId, FixedInstant),
            new MealieHousehold(Guid.Parse("00000000-0000-0000-0000-000000000082"), "Holm", Guid.Parse("00000000-0000-0000-0000-000000000083"), "holm"),
            "buddy-sync",
            "v3.29.0",
            FixedGuardianId,
            FixedInstant),
        "Mealplans/MealieConnected.json");

    [Fact]
    public void MealieDisconnected() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new MealieDisconnected(FixedConnectionId, FixedGuardianId, FixedInstant),
        "Mealplans/MealieDisconnected.json");

    [Fact]
    public void MealLinkedToRecipe() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new MealLinkedToRecipe(FixedMealId, FixedRecipe, FixedGuardianId, FixedInstant),
        "Mealplans/MealLinkedToRecipe.json");

    [Fact]
    public void MealRecipeUnlinked() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new MealRecipeUnlinked(FixedMealId, FixedGuardianId, FixedInstant),
        "Mealplans/MealRecipeUnlinked.json");
}
