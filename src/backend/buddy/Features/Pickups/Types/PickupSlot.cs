namespace buddy.Features.Pickups;

// Declaration order doubles as display/sort order for a day's entries -- see
// PickupScheduleExpansion. Fixed two-value shape, mirroring MealSlot, rather than an open list of
// named times -- covers the twice-daily school/daycare run; see
// docs/backend/analysis/pickup-schedules.md#remaining-open-questions for why more than two slots
// a day is deferred.
public enum PickupSlot
{
    DropOff,
    PickUp
}
