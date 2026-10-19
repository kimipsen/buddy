using System.Text.Json;

using FluentValidation.Results;

namespace buddy.Common.Validation;

// A field -> messages map, matching FluentValidation's own ValidationResult.ToDictionary() shape
// and the "details" field of the error envelope in docs/backend/http-status-codes.md. A message
// with no single associated field (e.g. a resend-cooldown rejection) goes under "", the same
// convention ASP.NET Core's ModelStateDictionary uses for a non-field-specific error.
//
// Keys are camelCase JSON paths of the request, the same as binding errors carry: every key passes
// through NormalizeKey here, so a validator's C# property path (`Days[0].Week`) and a handler's
// explicit key (`ServedWithin`) both reach the client as `days[0].week` / `servedWithin`. A rule
// whose C# path differs from the request field (a value object's `.Value`, a renamed property)
// still needs OverridePropertyName with the request's name.
public sealed record ValidationProblem
{
    public ValidationProblem(IReadOnlyDictionary<string, string[]> errors)
    {
        Errors = errors
            .GroupBy(entry => NormalizeKey(entry.Key), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.SelectMany(entry => entry.Value).ToArray(), StringComparer.Ordinal);
    }

    // For keys that already are the request's JSON paths (RequestBindingFailureMiddleware): kept
    // verbatim, so a dictionary key the client sent (`policy.Owner`) isn't re-cased.
    private ValidationProblem(IReadOnlyDictionary<string, string[]> errors, bool _) => Errors = errors;

    internal static ValidationProblem FromJsonPaths(IReadOnlyDictionary<string, string[]> errors) => new(errors, true);

    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public static ValidationProblem Of(string message) =>
        new(new Dictionary<string, string[]> { [""] = [message] });

    public static ValidationProblem FromFluentValidation(ValidationResult result) =>
        new(new Dictionary<string, string[]>(result.ToDictionary()));

    // Camel-cases the name of each dot-separated segment with the HTTP JSON naming policy and keeps
    // its indexers: `Recurrence.Weekdays[1]` -> `recurrence.weekdays[1]`, `""` stays `""`.
    internal static string NormalizeKey(string key) =>
        string.Join('.', key.Split('.').Select(segment =>
        {
            var indexer = segment.IndexOf('[', StringComparison.Ordinal);
            return indexer < 0
                ? JsonNamingPolicy.CamelCase.ConvertName(segment)
                : JsonNamingPolicy.CamelCase.ConvertName(segment[..indexer]) + segment[indexer..];
        }));
}
