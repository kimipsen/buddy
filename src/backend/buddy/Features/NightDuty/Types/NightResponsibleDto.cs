using System.Diagnostics;
using System.Text.Json.Serialization;

using buddy.Common;
using buddy.Features.Babysitters;
using buddy.Features.Users;
using buddy.Serialization;

namespace buddy.Features.NightDuty;

// The wire discriminator of NightResponsibleDto ("kind"), as a numeric ordinal like every other
// kind on this API. The domain uses the NightResponsible union instead.
public enum NightResponsibleKind
{
    Guardian,
    Babysitter,
    Sleepover,
    Unassigned
}

// The HTTP shape of a NightResponsible: an object whose numeric "kind" picks the case, with only
// that case's fields beside it -- e.g. { "kind": 0, "guardianId": "..." } or
// { "kind": 2, "hostName": "...", "location": "", "contactInfo": "" }. A babysitter's "name" is
// output only: ignored on the way in, resolved through BabysitterNames on the way out. Same shape
// as PickupAssigneeDto.
[JsonConverter(typeof(NightResponsibleDtoJsonConverter))]
public abstract record NightResponsibleDto
{
    public NightResponsible ToDomain() => this switch
    {
        GuardianNightResponsibleDto guardian => new NightResponsible.Guardian(new UserId(guardian.GuardianId)),
        BabysitterNightResponsibleDto babysitter => new NightResponsible.Babysitter(new UserId(babysitter.GuardianId), new BabysitterId(babysitter.BabysitterId)),
        SleepoverNightResponsibleDto sleepover => new NightResponsible.Sleepover(
            FreeText.Normalize(sleepover.HostName), FreeText.Normalize(sleepover.Location), FreeText.Normalize(sleepover.ContactInfo)),
        UnassignedNightResponsibleDto => new NightResponsible.Unassigned(),
        _ => throw new UnreachableException($"Unmapped NightResponsibleDto case: {GetType().Name}."),
    };

    public static NightResponsibleDto FromDomain(NightResponsible responsible, BabysitterNames babysitterNames) => responsible switch
    {
        NightResponsible.Guardian guardian => new GuardianNightResponsibleDto(guardian.GuardianId.Value),
        NightResponsible.Babysitter babysitter => new BabysitterNightResponsibleDto(
            babysitter.GuardianId.Value, babysitter.BabysitterId.Value, babysitterNames.NameOf(babysitter.BabysitterId)),
        NightResponsible.Sleepover sleepover => new SleepoverNightResponsibleDto(sleepover.HostName, sleepover.Location, sleepover.ContactInfo),
        NightResponsible.Unassigned => new UnassignedNightResponsibleDto(),
    };
}

public sealed record GuardianNightResponsibleDto(Guid GuardianId) : NightResponsibleDto;

// Name is output only ("" when the babysitter can't be resolved any more); a value sent in is ignored.
public sealed record BabysitterNightResponsibleDto(Guid GuardianId, Guid BabysitterId, string? Name = null) : NightResponsibleDto;

// Location and ContactInfo are optional on the way in; they always come back, "" meaning none.
public sealed record SleepoverNightResponsibleDto(string HostName, string? Location = null, string? ContactInfo = null) : NightResponsibleDto;

public sealed record UnassignedNightResponsibleDto : NightResponsibleDto;

public sealed class NightResponsibleDtoJsonConverter : KindDiscriminatedJsonConverter<NightResponsibleDto>
{
    protected override IReadOnlyDictionary<int, Type> Cases { get; } = new Dictionary<int, Type>
    {
        [(int)NightResponsibleKind.Guardian] = typeof(GuardianNightResponsibleDto),
        [(int)NightResponsibleKind.Babysitter] = typeof(BabysitterNightResponsibleDto),
        [(int)NightResponsibleKind.Sleepover] = typeof(SleepoverNightResponsibleDto),
        [(int)NightResponsibleKind.Unassigned] = typeof(UnassignedNightResponsibleDto),
    };
}

// The HTTP shape of a NightEntry. Note is optional on the way in ("" = none) and always comes back.
public sealed record NightEntryDto(NightResponsibleDto Responsible, string? Note = null)
{
    public NightEntry ToDomain() => new(Responsible.ToDomain(), FreeText.Normalize(Note));

    public static NightEntryDto FromDomain(NightEntry entry, BabysitterNames babysitterNames) =>
        new(NightResponsibleDto.FromDomain(entry.Responsible, babysitterNames), entry.Note);
}
