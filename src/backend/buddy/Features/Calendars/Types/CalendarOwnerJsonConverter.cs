using System.Text.Json;
using System.Text.Json.Serialization;

using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Calendars;

// CalendarOwner.User(UserId Value) and CalendarOwner.Group(GroupId Value) each wrap exactly one
// id, so once serialized (both UserId and GroupId flatten to a bare Guid via
// StronglyTypedIdJsonConverterFactory) their JSON shapes are identical: {"Value": "<guid>"}.
// System.Text.Json's built-in union converter distinguishes cases by JSON shape and throws
// ("JSON value type 'Object' is ambiguous for union type... multiple case types can use this
// value type") the moment it actually has to round-trip CalendarOwner -- which only started
// happening once CalendarSnapshot (see CalendarSnapshotProjection) made Calendar itself a
// Marten-stored, JSON-serialized document; CalendarOwner was never serialized before (it isn't
// part of any event payload, and CalendarResponseDto never exposes it). Pickups/Types/
// PickupAssigneeKind.cs hit the identical ambiguity earlier and deliberately avoided a union
// altogether for that case; here CalendarOwner's shape is public API used throughout
// CalendarAuthorization, so a small explicit converter (Kind discriminator + raw Guid) is the
// narrower fix -- same technique as a hand-rolled discriminated payload, just scoped to
// serialization instead of reshaping the domain type.
public sealed class CalendarOwnerJsonConverter : JsonConverter<CalendarOwner>
{
    public override CalendarOwner Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var kind = root.GetProperty("Kind").GetString();
        var id = root.GetProperty("Id").GetGuid();

        return kind switch
        {
            nameof(CalendarOwner.User) => new CalendarOwner.User(new UserId(id)),
            nameof(CalendarOwner.Group) => new CalendarOwner.Group(new GroupId(id)),
            _ => throw new JsonException($"Unknown CalendarOwner Kind discriminator: '{kind}'.")
        };
    }

    public override void Write(Utf8JsonWriter writer, CalendarOwner value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        switch (value)
        {
            case CalendarOwner.User user:
                writer.WriteString("Kind", nameof(CalendarOwner.User));
                writer.WriteString("Id", user.Value.Value);
                break;

            case CalendarOwner.Group group:
                writer.WriteString("Kind", nameof(CalendarOwner.Group));
                writer.WriteString("Id", group.Value.Value);
                break;
        }

        writer.WriteEndObject();
    }
}
