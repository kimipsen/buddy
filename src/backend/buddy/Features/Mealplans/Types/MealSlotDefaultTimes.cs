namespace buddy.Features.Mealplans;

// Fallback used by the iCal feed wherever a MealPlan hasn't configured its own SlotTimes entry for
// a slot -- never written to the aggregate itself. Generally-accepted mealtimes, not a claim about
// any specific family's actual schedule.
public static class MealSlotDefaultTimes
{
    public static readonly IReadOnlyDictionary<MealSlot, TimeOnly> Values = new Dictionary<MealSlot, TimeOnly>
    {
        [MealSlot.Breakfast] = new TimeOnly(7, 0),
        [MealSlot.Lunch] = new TimeOnly(12, 0),
        [MealSlot.Dinner] = new TimeOnly(18, 0),
        [MealSlot.Snack] = new TimeOnly(15, 0),
    };
}
