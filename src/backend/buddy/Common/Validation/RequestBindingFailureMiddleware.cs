using System.Text.Json;
using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace buddy.Common.Validation;

// Renders a request minimal APIs couldn't bind -- unreadable JSON, a required field missing
// (RespectRequiredConstructorParameters), null for a non-nullable field (RespectNullableAnnotations),
// a missing body or route/query value -- as the ErrorEnvelope a validator failure gets, so clients
// parse one 400 shape. Needs RouteHandlerOptions.ThrowOnBadRequest (Program.cs): without it the
// framework writes a bodiless 400 and never throws. One middleware, like ConcurrencyConflictMiddleware,
// because the failure happens before any endpoint code runs.
public sealed partial class RequestBindingFailureMiddleware(RequestDelegate next, IOptions<JsonOptions> jsonOptions, ILogger<RequestBindingFailureMiddleware> logger)
{
    internal const string UnbindableRequestMessage = "A route, query or body value is missing or malformed.";

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (BadHttpRequestException exception) when (!context.Response.HasStarted)
        {
            logger.UnbindableRequest(exception, context.Request.Method, context.Request.Path.Value);

            context.Response.Clear();
            context.Response.StatusCode = exception.StatusCode;

            // 413/415 and the like keep the framework's status with no body, as before.
            if (exception.StatusCode != StatusCodes.Status400BadRequest)
            {
                return;
            }

            // A non-JSON failure (route/query value, missing body) gets a fixed message: the
            // framework's own names the C# parameter type and echoes the raw input back.
            var problem = exception.InnerException is JsonException json
                ? FromJsonException(json)
                : ValidationProblem.Of(UnbindableRequestMessage);

            await context.Response.WriteAsJsonAsync(problem.ToEnvelope(context), jsonOptions.Value.SerializerOptions, context.RequestAborted);
        }
    }

    // JsonException carries the failing location as a JSONPath ("$.days[0].week") but no
    // structured list of missing properties -- those only appear in the message, so they are read
    // from there and the generic per-path message is the fallback.
    internal static ValidationProblem FromJsonException(JsonException exception)
    {
        var field = FieldFromPath(exception.Path);

        if (MissingRequiredProperties().Match(exception.Message) is { Success: true } match)
        {
            var errors = QuotedName().Matches(match.Groups["names"].Value)
                .Select(name => Combine(field, name.Groups["name"].Value))
                .Distinct()
                .ToDictionary(name => name, name => new[] { $"The {name} field is required." });

            if (errors.Count > 0)
            {
                return new ValidationProblem(errors);
            }
        }

        var message = field.Length == 0
            ? "The request body is not valid JSON for this endpoint."
            : $"The {field} field is missing, null or has the wrong type.";

        return new ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });
    }

    private static string FieldFromPath(string? path) =>
        path is null or "$" ? "" : path.StartsWith("$.", StringComparison.Ordinal) ? path[2..] : path.TrimStart('$');

    private static string Combine(string parent, string name) => parent.Length == 0 ? name : $"{parent}.{name}";

    [GeneratedRegex("missing required properties including: (?<names>.+?)\\.?$", RegexOptions.CultureInvariant)]
    private static partial Regex MissingRequiredProperties();

    [GeneratedRegex("'(?<name>[^']+)'", RegexOptions.CultureInvariant)]
    private static partial Regex QuotedName();
}

public static class RequestBindingFailureMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestBindingFailures(this IApplicationBuilder app) =>
        app.UseMiddleware<RequestBindingFailureMiddleware>();
}
