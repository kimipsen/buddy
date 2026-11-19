using System.Text.Json.Nodes;

namespace buddy.Features.Mcp;

// Other people's text reaches the guardian's LLM through tool results (analysis Question 8). Most of
// these fields have no length cap in the validators, so the cap is applied here, on the way out: a
// field longer than its cap is cut and the object that holds it gets "truncated": true.
public static class McpTextShaping
{
    public const int TitleLimit = 200;
    public const int NotesLimit = 500;

    // Camel-cased JSON property names, as the HTTP serializer writes them.
    private static readonly Dictionary<string, int> Limits = new(StringComparer.Ordinal)
    {
        ["title"] = TitleLimit,
        ["name"] = TitleLimit,
        ["notes"] = NotesLimit,
        ["note"] = NotesLimit,
        ["description"] = NotesLimit,
        ["comment"] = NotesLimit,
        ["text"] = NotesLimit,
        ["sleepHygieneNotes"] = NotesLimit,
    };

    public static void Cap(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                CapObject(obj);
                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    Cap(item);
                }

                break;
        }
    }

    private static void CapObject(JsonObject obj)
    {
        var truncated = false;

        foreach (var (key, value) in obj.ToList())
        {
            if (value is JsonValue jsonValue
                && Limits.TryGetValue(key, out var limit)
                && jsonValue.TryGetValue<string>(out var text)
                && text.Length > limit)
            {
                obj[key] = Truncate(text, limit);
                truncated = true;
            }
            else
            {
                Cap(value);
            }
        }

        if (truncated)
        {
            obj["truncated"] = true;
        }
    }

    private static string Truncate(string text, int limit)
    {
        // Don't cut a surrogate pair in half.
        var length = char.IsHighSurrogate(text[limit - 1]) ? limit - 1 : limit;
        return text[..length];
    }
}
