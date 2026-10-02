using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

using buddy.Common;
using buddy.Features.Users;

namespace buddy.Features.Pickups;

// The HTTP shape of a PickupAssignee: an object whose numeric "kind" (PickupAssigneeKind) picks
// the case, with only that case's fields beside it -- e.g. { "kind": 0, "guardianId": "..." } or
// { "kind": 3, "hostName": "...", "location": "", "contactInfo": "" }. PickupAssigneeDtoJsonConverter
// reads and writes it; a missing or unknown kind fails request binding (400 validation_error).
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

// Hand-written rather than [JsonPolymorphic]: with an abstract base, a body missing "kind" makes
// System.Text.Json throw NotSupportedException, which minimal APIs don't treat as a binding failure
// (a 500, not a 400). The case's own fields still go through the configured options, so naming
// policy and RespectRequiredConstructorParameters apply as for any other request record.
public sealed class PickupAssigneeDtoJsonConverter : JsonConverter<PickupAssigneeDto>
{
    private const string KindProperty = "kind";

    public override PickupAssigneeDto Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("An assignee must be a JSON object.");
        }

        var kind = root.EnumerateObject()
            .Where(property => string.Equals(property.Name, KindProperty, StringComparison.OrdinalIgnoreCase))
            .Select(property => property.Value.TryGetInt32(out var value) ? (int?)value : null)
            // Last one wins on a duplicate "kind", the same as System.Text.Json's own object binding.
            .LastOrDefault()
            ?? throw new JsonException("An assignee requires a numeric kind.");

        return kind switch
        {
            (int)PickupAssigneeKind.Guardian => Deserialize<GuardianAssigneeDto>(root, options),
            (int)PickupAssigneeKind.SelfEscort => new SelfEscortAssigneeDto(),
            (int)PickupAssigneeKind.Sibling => Deserialize<SiblingAssigneeDto>(root, options),
            (int)PickupAssigneeKind.Playdate => Deserialize<PlaydateAssigneeDto>(root, options),
            _ => throw new JsonException($"Unknown assignee kind: {kind}."),
        };
    }

    public override void Write(Utf8JsonWriter writer, PickupAssigneeDto value, JsonSerializerOptions options)
    {
        var kind = value switch
        {
            GuardianAssigneeDto => PickupAssigneeKind.Guardian,
            SelfEscortAssigneeDto => PickupAssigneeKind.SelfEscort,
            SiblingAssigneeDto => PickupAssigneeKind.Sibling,
            PlaydateAssigneeDto => PickupAssigneeKind.Playdate,
            _ => throw new UnreachableException($"Unmapped PickupAssigneeDto case: {value.GetType().Name}."),
        };

        writer.WriteStartObject();
        writer.WriteNumber(options.PropertyNamingPolicy?.ConvertName(KindProperty) ?? KindProperty, (int)kind);

        foreach (var property in JsonSerializer.SerializeToElement(value, value.GetType(), options).EnumerateObject())
        {
            property.WriteTo(writer);
        }

        writer.WriteEndObject();
    }

    // The ignored "kind" property is skipped as an unmapped member; the case's own fields bind as usual.
    // A failure is rethrown without its (element-relative) path, so the outer serializer stamps the
    // real one ("$.assignee") and RequestBindingFailureMiddleware reports e.g. "assignee.guardianId".
    private static T Deserialize<T>(JsonElement root, JsonSerializerOptions options) where T : PickupAssigneeDto
    {
        try
        {
            return root.Deserialize<T>(options) ?? throw new JsonException($"An assignee of type {typeof(T).Name} was null.");
        }
        catch (JsonException exception)
        {
            throw new JsonException(exception.Message, exception);
        }
    }
}
