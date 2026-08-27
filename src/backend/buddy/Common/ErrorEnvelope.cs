using buddy.Common.Validation;

namespace buddy.Common;

// The structured error body docs/backend/http-status-codes.md's "Error Response Shape
// (Recommended)" section calls for. Only Result<T>.Validation (and its feature-specific copies)
// render through this -- NotFound/Forbidden keep their existing, endpoint-specific mappings.
public sealed record ErrorEnvelope(string Code, string Message, IReadOnlyDictionary<string, string[]> Details, string RequestId);

public static class ValidationProblemExtensions
{
    public static ErrorEnvelope ToEnvelope(this ValidationProblem problem, HttpContext context) =>
        new("validation_error", "One or more fields are invalid.", problem.Errors, context.TraceIdentifier);
}
