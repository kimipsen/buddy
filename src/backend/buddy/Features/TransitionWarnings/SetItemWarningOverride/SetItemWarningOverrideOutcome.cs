using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

namespace buddy.Features.TransitionWarnings;

// What SetItemWarningOverride can answer. The cap conflict only happens here, so it is its own
// union rather than a new Result<T> case (same choice as ChecklistSuggestionOutcome).
public union SetItemWarningOverrideOutcome(
    SetItemWarningOverrideOutcome.Success,
    SetItemWarningOverrideOutcome.NotFound,
    SetItemWarningOverrideOutcome.Forbidden,
    SetItemWarningOverrideOutcome.Validation,
    SetItemWarningOverrideOutcome.TooManyOverrides)
{
    public const string TooManyOverridesCode = "too_many_warning_overrides";

    public sealed record Success(ChildTransitionWarningsResponse Settings);
    public sealed record NotFound;
    public sealed record Forbidden;
    public sealed record Validation(buddy.Common.Validation.ValidationProblem Problem);
    public sealed record TooManyOverrides;

    public static SetItemWarningOverrideOutcome Denied(TransitionWarningsAccess access) => access.ToDeniedResult<Unit>() switch
    {
        Result<Unit>.Forbidden => new Forbidden(),
        Result<Unit>.NotFound or Result<Unit>.Success or Result<Unit>.Validation => new NotFound(),
    };

    public Results<Ok<ChildTransitionWarningsResponse>, Microsoft.AspNetCore.Http.HttpResults.NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>, Conflict<ErrorEnvelope>> ToHttpResult(HttpContext httpContext) => this switch
    {
        Success(var settings) => TypedResults.Ok(settings),
        NotFound => TypedResults.NotFound(),
        Forbidden => TypedResults.Forbid(),
        Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
        TooManyOverrides => TypedResults.Conflict(new ErrorEnvelope(
            TooManyOverridesCode,
            $"A child can have at most {LeadTimeRules.MaxOverrides} exceptions. Remove an old one first.",
            new Dictionary<string, string[]>(),
            httpContext.TraceIdentifier)),
    };
}
