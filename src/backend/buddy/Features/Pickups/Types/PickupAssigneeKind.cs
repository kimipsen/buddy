namespace buddy.Features.Pickups;

// The wire discriminator of PickupAssigneeDto ("kind"), as a numeric ordinal like every other enum
// on this API. The domain uses the PickupAssignee union instead.
public enum PickupAssigneeKind
{
    Guardian,
    SelfEscort,
    Sibling,
    Playdate
}
