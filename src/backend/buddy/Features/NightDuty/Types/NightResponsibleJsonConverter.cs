using System.Text.Json;
using System.Text.Json.Serialization;

using buddy.Features.Babysitters;
using buddy.Features.Users;

namespace buddy.Features.NightDuty;

// Marten's (de)serialization of NightResponsible inside the night duty events and the NightSchedule
// snapshot, with an explicit Kind discriminator: Guardian and Babysitter both carry bare guids, so
// the union's shape-based classifier can't tell them apart -- the same fix as
// PickupAssigneeJsonConverter.
public sealed class NightResponsibleJsonConverter : JsonConverter<NightResponsible>
{
    // Property names of the persisted shape -- part of the stored event contract (golden files).
    private const string KindProperty = "Kind";
    private const string GuardianIdProperty = "GuardianId";
    private const string BabysitterIdProperty = "BabysitterId";
    private const string HostNameProperty = "HostName";
    private const string LocationProperty = "Location";
    private const string ContactInfoProperty = "ContactInfo";

    public override NightResponsible Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var kind = String(root, KindProperty);

        return kind switch
        {
            nameof(NightResponsible.Guardian) => new NightResponsible.Guardian(new UserId(Required(root, GuardianIdProperty).GetGuid())),
            nameof(NightResponsible.Babysitter) => new NightResponsible.Babysitter(
                new UserId(Required(root, GuardianIdProperty).GetGuid()), new BabysitterId(Required(root, BabysitterIdProperty).GetGuid())),
            nameof(NightResponsible.Sleepover) => new NightResponsible.Sleepover(String(root, HostNameProperty), String(root, LocationProperty), String(root, ContactInfoProperty)),
            nameof(NightResponsible.Unassigned) => new NightResponsible.Unassigned(),
            _ => throw new JsonException($"Unknown NightResponsible Kind discriminator: '{kind}'.")
        };
    }

    public override void Write(Utf8JsonWriter writer, NightResponsible value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        switch (value)
        {
            case NightResponsible.Guardian guardian:
                writer.WriteString(KindProperty, nameof(NightResponsible.Guardian));
                writer.WriteString(GuardianIdProperty, guardian.GuardianId.Value);
                break;

            case NightResponsible.Babysitter babysitter:
                writer.WriteString(KindProperty, nameof(NightResponsible.Babysitter));
                writer.WriteString(GuardianIdProperty, babysitter.GuardianId.Value);
                writer.WriteString(BabysitterIdProperty, babysitter.BabysitterId.Value);
                break;

            case NightResponsible.Sleepover sleepover:
                writer.WriteString(KindProperty, nameof(NightResponsible.Sleepover));
                writer.WriteString(HostNameProperty, sleepover.HostName);
                writer.WriteString(LocationProperty, sleepover.Location);
                writer.WriteString(ContactInfoProperty, sleepover.ContactInfo);
                break;

            case NightResponsible.Unassigned:
                writer.WriteString(KindProperty, nameof(NightResponsible.Unassigned));
                break;

            // A default(NightResponsible) holds no case: refuse it here rather than persisting an
            // object that can never be read back.
            default:
                throw new JsonException("Cannot serialize a NightResponsible that holds no case.");
        }

        writer.WriteEndObject();
    }

    private static JsonElement Required(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) ? value : throw new JsonException($"NightResponsible is missing '{name}'.");

    private static string String(JsonElement root, string name) =>
        Required(root, name).GetString() ?? throw new JsonException($"NightResponsible '{name}' is null.");
}
