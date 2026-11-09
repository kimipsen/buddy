using System.Text.Json.Serialization;

using buddy.Features.Babysitters;
using buddy.Features.Users;

namespace buddy.Features.NightDuty;

// Who is on duty for a night. The cases copy PickupAssignee's shapes (Playdate becomes Sleepover)
// but it is a separate union: SelfEscort/Sibling make no sense at night, and Unassigned (a note on
// its own) makes none for a pickup. Persisted through NightResponsibleJsonConverter (an explicit
// Kind discriminator: Guardian and Babysitter both flatten to bare guids). On the wire it is
// NightResponsibleDto. See docs/backend/analysis/night-duty.md, Question 3.
public union NightResponsible(NightResponsible.Guardian, NightResponsible.Babysitter, NightResponsible.Sleepover, NightResponsible.Unassigned)
{
    // An active guardian of the child.
    public sealed record Guardian(UserId GuardianId);

    // A babysitter on GuardianId's BabysitterList. The name is resolved on every read, so a rename
    // reaches every night (docs/backend/analysis/babysitters.md, Question 4).
    public sealed record Babysitter(UserId GuardianId, BabysitterId BabysitterId);

    // The child sleeps somewhere else. Location and ContactInfo are optional free text: "" = not given.
    public sealed record Sleepover(string HostName, string Location, string ContactInfo);

    // Nobody named -- only valid with a note ("Screen night").
    public sealed record Unassigned;
}

// One night's plan: the responsible person plus a short note ("" = none). Never Unassigned with an
// empty note -- that isn't an entry (a pattern leaves the day out, an override uses Cleared).
public sealed record NightEntry(NightResponsible Responsible, string Note)
{
    // Derived, so kept out of the persisted event and snapshot JSON.
    [JsonIgnore]
    public bool IsEmpty => Responsible is NightResponsible.Unassigned && Note.Length == 0;
}
