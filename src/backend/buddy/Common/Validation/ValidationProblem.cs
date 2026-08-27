using FluentValidation.Results;

namespace buddy.Common.Validation;

// A field -> messages map, matching FluentValidation's own ValidationResult.ToDictionary() shape
// and the "details" field of the error envelope in docs/backend/http-status-codes.md. A message
// with no single associated field (e.g. a resend-cooldown rejection) goes under "", the same
// convention ASP.NET Core's ModelStateDictionary uses for a non-field-specific error.
public sealed record ValidationProblem(IReadOnlyDictionary<string, string[]> Errors)
{
    public static ValidationProblem Of(string message) =>
        new(new Dictionary<string, string[]> { [""] = [message] });

    public static ValidationProblem FromFluentValidation(ValidationResult result) =>
        new(new Dictionary<string, string[]>(result.ToDictionary()));
}
