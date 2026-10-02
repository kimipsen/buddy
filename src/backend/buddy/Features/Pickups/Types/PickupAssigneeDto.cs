using System.Diagnostics;
using System.Text.Json.Serialization;

using buddy.Common;
using buddy.Features.Users;
using buddy.Serialization;

namespace buddy.Features.Pickups;

// The HTTP shape of a PickupAssignee: an object whose numeric "kind" (PickupAssigneeKind) picks
// the case, with only that case's fields beside it -- e.g. { "kind": 0, "guardianId": "..." } or
// { "kind": 3, "hostName": "...", "location": "", "contactInfo": "" }. PickupAssigneeDtoJsonConverter
// (a KindDiscriminatedJsonConverter) reads and writes it; a missing or unknown kind fails request
// binding (400 validation_error).
[JsonConverter(typeof(PickupAssigneeDtoJsonConverter))]
public abstract record PickupAssigneeDto
{
    public PickupAssignee ToDomain() => this switch
    {
        GuardianAssigneeDto guardian => new PickupAssignee.Guardian(new UserId(guardian.GuardianId)),
        SelfEscortAssigneeDto => new PickupAssignee.SelfEscort(),
        SiblingAssigneeDto sibling => new PickupAssignee.Sibling(new UserId(sibling.SiblingChildId)),
        PlaydateAssigneeDto playdate => new PickupAssignee.Playdate(
            FreeText.Normalize(playdate.HostName), FreeText.Normalize(playdate.Location), FreeText.Normalize(playdate.ContactInfo)),
        _ => throw new UnreachableException($"Unmapped PickupAssigneeDto case: {GetType().Name}."),
    };

    public static PickupAssigneeDto FromDomain(PickupAssignee assignee) => assignee switch
    {
        PickupAssignee.Guardian guardian => new GuardianAssigneeDto(guardian.GuardianId.Value),
        PickupAssignee.SelfEscort => new SelfEscortAssigneeDto(),
        PickupAssignee.Sibling sibling => new SiblingAssigneeDto(sibling.SiblingChildId.Value),
        PickupAssignee.Playdate playdate => new PlaydateAssigneeDto(playdate.HostName, playdate.Location, playdate.ContactInfo),
    };
}

public sealed record GuardianAssigneeDto(Guid GuardianId) : PickupAssigneeDto;

public sealed record SelfEscortAssigneeDto : PickupAssigneeDto;

public sealed record SiblingAssigneeDto(Guid SiblingChildId) : PickupAssigneeDto;

// Location and ContactInfo are optional on the way in; they always come back, "" meaning none.
public sealed record PlaydateAssigneeDto(string HostName, string? Location = null, string? ContactInfo = null) : PickupAssigneeDto;

public sealed class PickupAssigneeDtoJsonConverter : KindDiscriminatedJsonConverter<PickupAssigneeDto>
{
    protected override IReadOnlyDictionary<int, Type> Cases { get; } = new Dictionary<int, Type>
    {
        [(int)PickupAssigneeKind.Guardian] = typeof(GuardianAssigneeDto),
        [(int)PickupAssigneeKind.SelfEscort] = typeof(SelfEscortAssigneeDto),
        [(int)PickupAssigneeKind.Sibling] = typeof(SiblingAssigneeDto),
        [(int)PickupAssigneeKind.Playdate] = typeof(PlaydateAssigneeDto),
    };
}
