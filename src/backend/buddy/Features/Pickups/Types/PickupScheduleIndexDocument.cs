namespace buddy.Features.Pickups;

// Resolves "what is child X's PickupSchedule stream ID" -- a PickupSchedule is a 1:1 singleton per
// child, but Marten streams are still addressed by their own aggregate ID, so this lookup is
// needed the same way MealPlanIndexDocument is needed for MealPlan. Written once on
// PickupScheduleCreated, never updated or removed afterwards.
public sealed record PickupScheduleIndexDocument(Guid Id, Guid ChildId);
