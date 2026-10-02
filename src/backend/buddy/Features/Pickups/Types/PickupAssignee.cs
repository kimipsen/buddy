using buddy.Features.Users;

namespace buddy.Features.Pickups;

// Who handles a slot. Each case carries exactly the data its kind needs, so a Guardian can't
// carry a stray playdate host name and a Playdate can't lack one -- what the flat
// Kind-plus-optional-fields record used to leave to the validator. Persisted inside
// PickupAssigned/PickupCleared through PickupAssigneeJsonConverter (an explicit Kind
// discriminator: System.Text.Json's union converter can only tell cases apart by JSON shape).
// On the wire it is PickupAssigneeDto. See docs/backend/analysis/eliminate-nulls.md, Phase 5.1.
public union PickupAssignee(PickupAssignee.Guardian, PickupAssignee.SelfEscort, PickupAssignee.Sibling, PickupAssignee.Playdate)
{
    // A specific guardian of the child handles this slot themself.
    public sealed record Guardian(UserId GuardianId);

    // The child goes by themself.
    public sealed record SelfEscort;

    // Another of the child's own siblings escorts them.
    public sealed record Sibling(UserId SiblingChildId);

    // Someone outside the family and the app's user model (e.g. a friend's parent). Location and
    // ContactInfo are optional free text: "" means not given.
    public sealed record Playdate(string HostName, string Location, string ContactInfo);
}
