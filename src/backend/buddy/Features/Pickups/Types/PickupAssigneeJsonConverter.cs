using System.Text.Json;
using System.Text.Json.Serialization;

using buddy.Features.Babysitters;
using buddy.Features.Users;

namespace buddy.Features.Pickups;

// Marten's (de)serialization of PickupAssignee inside PickupAssigned/PickupCleared and the
// PickupSchedule snapshot. The Guardian and Sibling cases both wrap one id that flattens to a bare
// Guid, so the union's shape-based classifier can't tell them apart; an explicit Kind
// discriminator can -- the same fix as PrintTemplateOwnerJsonConverter.
public sealed class PickupAssigneeJsonConverter : JsonConverter<PickupAssignee>
{
    public override PickupAssignee Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var kind = String(root, "Kind");

        return kind switch
        {
            nameof(PickupAssignee.Guardian) => new PickupAssignee.Guardian(new UserId(Required(root, "GuardianId").GetGuid())),
            nameof(PickupAssignee.SelfEscort) => new PickupAssignee.SelfEscort(),
            nameof(PickupAssignee.Sibling) => new PickupAssignee.Sibling(new UserId(Required(root, "SiblingChildId").GetGuid())),
            nameof(PickupAssignee.Playdate) => new PickupAssignee.Playdate(String(root, "HostName"), String(root, "Location"), String(root, "ContactInfo")),
            nameof(PickupAssignee.Babysitter) => new PickupAssignee.Babysitter(
                new UserId(Required(root, "GuardianId").GetGuid()), new BabysitterId(Required(root, "BabysitterId").GetGuid())),
            _ => throw new JsonException($"Unknown PickupAssignee Kind discriminator: '{kind}'.")
        };
    }

    public override void Write(Utf8JsonWriter writer, PickupAssignee value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        switch (value)
        {
            case PickupAssignee.Guardian guardian:
                writer.WriteString("Kind", nameof(PickupAssignee.Guardian));
                writer.WriteString("GuardianId", guardian.GuardianId.Value);
                break;

            case PickupAssignee.SelfEscort:
                writer.WriteString("Kind", nameof(PickupAssignee.SelfEscort));
                break;

            case PickupAssignee.Sibling sibling:
                writer.WriteString("Kind", nameof(PickupAssignee.Sibling));
                writer.WriteString("SiblingChildId", sibling.SiblingChildId.Value);
                break;

            case PickupAssignee.Playdate playdate:
                writer.WriteString("Kind", nameof(PickupAssignee.Playdate));
                writer.WriteString("HostName", playdate.HostName);
                writer.WriteString("Location", playdate.Location);
                writer.WriteString("ContactInfo", playdate.ContactInfo);
                break;

            case PickupAssignee.Babysitter babysitter:
                writer.WriteString("Kind", nameof(PickupAssignee.Babysitter));
                writer.WriteString("GuardianId", babysitter.GuardianId.Value);
                writer.WriteString("BabysitterId", babysitter.BabysitterId.Value);
                break;

            // A default(PickupAssignee) holds no case: refuse it here rather than persisting an
            // object that can never be read back.
            default:
                throw new JsonException("Cannot serialize a PickupAssignee that holds no case.");
        }

        writer.WriteEndObject();
    }

    private static JsonElement Required(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) ? value : throw new JsonException($"PickupAssignee is missing '{name}'.");

    private static string String(JsonElement root, string name) =>
        Required(root, name).GetString() ?? throw new JsonException($"PickupAssignee '{name}' is null.");
}
