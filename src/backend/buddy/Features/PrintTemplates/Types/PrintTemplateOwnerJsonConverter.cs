using System.Text.Json;
using System.Text.Json.Serialization;

using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.PrintTemplates;

// Both cases wrap exactly one id that flattens to a bare Guid, so System.Text.Json's union
// converter can't tell them apart (it classifies union cases by JSON shape). The fix: a Kind
// discriminator plus the raw Guid.
public sealed class PrintTemplateOwnerJsonConverter : JsonConverter<PrintTemplateOwner>
{
    public override PrintTemplateOwner Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var kind = root.GetProperty("Kind").GetString();
        var id = root.GetProperty("Id").GetGuid();

        return kind switch
        {
            nameof(PrintTemplateOwner.User) => new PrintTemplateOwner.User(new UserId(id)),
            nameof(PrintTemplateOwner.Group) => new PrintTemplateOwner.Group(new GroupId(id)),
            _ => throw new JsonException($"Unknown PrintTemplateOwner Kind discriminator: '{kind}'.")
        };
    }

    public override void Write(Utf8JsonWriter writer, PrintTemplateOwner value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        switch (value)
        {
            case PrintTemplateOwner.User user:
                writer.WriteString("Kind", nameof(PrintTemplateOwner.User));
                writer.WriteString("Id", user.Value.Value);
                break;

            case PrintTemplateOwner.Group group:
                writer.WriteString("Kind", nameof(PrintTemplateOwner.Group));
                writer.WriteString("Id", group.Value.Value);
                break;
        }

        writer.WriteEndObject();
    }
}
