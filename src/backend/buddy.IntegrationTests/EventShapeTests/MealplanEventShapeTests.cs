using buddy.Features.Calendars;
using buddy.Features.Mealplans;
using buddy.Features.Users;

using Xunit;

namespace buddy.IntegrationTests.EventShapeTests;

public sealed class MealplanEventShapeTests
{
    private static readonly MealId FixedMealId = new(Guid.Parse("00000000-0000-0000-0000-000000000060"));
    private static readonly MealPlanId FixedMealPlanId = new(Guid.Parse("00000000-0000-0000-0000-000000000061"));
    private static readonly UserId FixedChildId = new(Guid.Parse("00000000-0000-0000-0000-000000000003"));
    private static readonly UserId FixedGuardianId = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    private static readonly DateTimeOffset FixedInstant = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly FixedDate = new(2025, 6, 1);

    [Fact]
    public void MealCreated() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new MealCreated(FixedMealId, FixedChildId, FixedGuardianId, "Tacos", "Ground beef, tortillas, salsa", Icon.New("taco"), Color.New("#ffaa00"), FixedInstant),
        "Mealplans/MealCreated.json");

    [Fact]
    public void MealDetailsUpdated() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new MealDetailsUpdated(
            FixedMealId,
            new MealDetails("Tacos", "Ground beef, tortillas, salsa", Icon.New("taco"), Color.New("#ffaa00")),
            new MealDetails("Tacos", "New recipe", Icon.New("taco"), Color.New("#ffaa00")),
            FixedGuardianId,
            FixedInstant),
        "Mealplans/MealDetailsUpdated.json");

    [Fact]
    public void MealArchived() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new MealArchived(FixedMealId, FixedGuardianId, FixedInstant),
        "Mealplans/MealArchived.json");

    [Fact]
    public void MealRated() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new MealRated(FixedMealId, FixedChildId, null, new MealRating(5, "Loved it!", FixedInstant), FixedInstant),
        "Mealplans/MealRated.json");

    [Fact]
    public void MealPlanCreated() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new MealPlanCreated(FixedMealPlanId, FixedChildId, FixedInstant),
        "Mealplans/MealPlanCreated.json");

    [Fact]
    public void MealAssignedToSlot() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new MealAssignedToSlot(
            FixedMealPlanId, FixedDate, MealSlot.Dinner,
            Before: null,
            After: new MealPlanAssignment(FixedMealId, FixedGuardianId, "No cilantro"),
            FixedInstant),
        "Mealplans/MealAssignedToSlot.json");

    [Fact]
    public void MealSlotCleared() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new MealSlotCleared(
            FixedMealPlanId, FixedDate, MealSlot.Dinner,
            new MealPlanAssignment(FixedMealId, FixedGuardianId, "No cilantro"),
            FixedGuardianId,
            FixedInstant),
        "Mealplans/MealSlotCleared.json");
}
