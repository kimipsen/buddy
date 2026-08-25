namespace buddy.Features.Pickups;

// The kind of principal responsible for a slot. A closed union (mirroring CalendarOwner) was
// considered and rejected here: the case payloads (GuardianId vs. SiblingChildId vs. the
// Playdate free-text fields) all serialize as a plain JSON object, and System.Text.Json's union
// converter can only tell cases apart by their JSON shape -- verified experimentally to throw
// ("JSON value type is ambiguous for union type... multiple case types can use this value type")
// without a custom JsonTypeClassifierFactory, a low-level mechanism with no precedent anywhere
// else in this codebase. Since PickupAssignment is embedded in persisted events, getting this
// wrong would mean Marten failing to replay history. A flat Kind discriminator plus per-case
// optional fields (PickupAssignment) avoids the ambiguity entirely and matches how every
// request/response DTO in this codebase already represents case-like data (e.g.
// MedicineScheduleResponse flattening Icon/Color).
public enum PickupAssigneeKind
{
    // A specific guardian of the child handles this slot themself.
    Guardian,
    // The child goes by themself.
    SelfEscort,
    // Another of the child's own siblings escorts them.
    Sibling,
    // Someone outside the family and the app's user model entirely (e.g. a friend's parent).
    Playdate
}
