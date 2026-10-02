using System.Text.Json;
using System.Text.Json.Serialization;

namespace buddy.Features.Users;

// Marten's (de)serialization of EmailVerification in the User snapshot, with an explicit Kind
// discriminator: {"Kind":"None"} or {"Kind":"Pending","TokenHash","RequestedAt","ExpiresAt"}.
public sealed class EmailVerificationJsonConverter : JsonConverter<EmailVerification>
{
    public override EmailVerification Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var kind = Required(root, "Kind").GetString();

        return kind switch
        {
            nameof(EmailVerification.None) => new EmailVerification.None(),
            nameof(EmailVerification.Pending) => new EmailVerification.Pending(
                Required(root, "TokenHash").GetString() ?? throw new JsonException("EmailVerification TokenHash is null."),
                Required(root, "RequestedAt").Deserialize<DateTimeOffset>(options),
                Required(root, "ExpiresAt").Deserialize<DateTimeOffset>(options)),
            _ => throw new JsonException($"Unknown EmailVerification Kind discriminator: '{kind}'."),
        };
    }

    public override void Write(Utf8JsonWriter writer, EmailVerification value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        switch (value)
        {
            case EmailVerification.None:
                writer.WriteString("Kind", nameof(EmailVerification.None));
                break;

            case EmailVerification.Pending pending:
                writer.WriteString("Kind", nameof(EmailVerification.Pending));
                writer.WriteString("TokenHash", pending.TokenHash);
                writer.WritePropertyName("RequestedAt");
                JsonSerializer.Serialize(writer, pending.RequestedAt, options);
                writer.WritePropertyName("ExpiresAt");
                JsonSerializer.Serialize(writer, pending.ExpiresAt, options);
                break;

            default:
                throw new JsonException("Cannot serialize an uninitialized EmailVerification.");
        }

        writer.WriteEndObject();
    }

    private static JsonElement Required(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
            ? value
            : throw new JsonException($"EmailVerification is missing its '{name}' property.");
}
