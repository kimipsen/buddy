using System.Text.Json;
using System.Text.Json.Nodes;

using buddy.Common;
using buddy.Common.Validation;

using ModelContextProtocol.Protocol;

namespace buddy.Features.Mcp;

// Maps a Result<T> (or a plain value) to an MCP tool result. Success is the value as structured
// content (an object: a list is wrapped as { "items": [...] }) plus the same JSON as text, with the
// user-written fields length-capped (McpTextShaping). Every failure is isError with an
// { error: { code, message, details } } text, using the ErrorEnvelope codes.
public static class McpResults
{
    public const string NotFoundCode = "not_found";

    public static CallToolResult From<T>(Result<T> result, JsonSerializerOptions options, Func<T, object>? project = null) => result switch
    {
        Result<T>.Success(var value) => Ok(project is null ? value : project(value), options),
        Result<T>.Validation(var problem) => Error(ValidationProblemExtensions.ErrorCode, "One or more fields are invalid.", problem.Errors),
        // NotFound and Forbidden collapse, as in the endpoints: the model can't tell private from missing.
        Result<T>.NotFound => NotFound(),
        Result<T>.Forbidden => NotFound(),
    };

    public static CallToolResult Ok(object? value, JsonSerializerOptions options)
    {
        var node = value is null ? new JsonObject() : JsonSerializer.SerializeToNode(value, value.GetType(), options) ?? new JsonObject();
        var structured = node as JsonObject ?? new JsonObject { ["items"] = node };
        McpTextShaping.Cap(structured);

        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = structured.ToJsonString(options) }],
            StructuredContent = JsonSerializer.SerializeToElement(structured, options),
        };
    }

    public static CallToolResult NotFound() => Error(NotFoundCode, "Not found.");

    public static CallToolResult Invalid(string message) =>
        From<object>(new Result<object>.Validation(ValidationProblem.Of(message)), JsonSerializerOptions.Web);

    public static CallToolResult Error(string code, string message, IReadOnlyDictionary<string, string[]>? details = null) => new()
    {
        IsError = true,
        Content =
        [
            new TextContentBlock
            {
                Text = JsonSerializer.Serialize(
                    new { error = new { code, message, details = details ?? new Dictionary<string, string[]>() } },
                    JsonSerializerOptions.Web)
            }
        ],
    };

    // The audit outcome of a result, for McpLog.
    public static string Outcome(CallToolResult result) => result.IsError == true ? "error" : "ok";
}
