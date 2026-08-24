namespace buddy.Features.Mealplans;

// Queryable read-model index letting a group-keyed route resolve which MealPlan (and which
// family, via AnchorChildId) a group currently has access to -- mirrors GroupOwnedCalendarDocument.
// Id is the MealPlanId itself (Marten identity convention -- see MealPlanIndexDocument), so
// re-sharing with a different group is a plain upsert, and unsharing deletes this row. Written by
// MartenMealPlanEventStore.AppendAsync on MealPlanSharedWithGroup, deleted on
// MealPlanUnsharedFromGroup.
public sealed record GroupSharedMealPlanDocument(Guid Id, Guid GroupId, Guid AnchorChildId);
