using buddy.Common;
using buddy.Common.OpenApi;
using buddy.Common.Validation;

using Http = Microsoft.AspNetCore.Http.HttpResults;

namespace buddy.Features.Mealplans;

// What a slice that talks to Mealie returns: Result<T>'s four cases plus the two that don't fit
// them -- a family with no connection (409 mealie_not_connected) and Mealie itself failing
// (502 mealie_unreachable / mealie_token_rejected / mealie_error). Not 503
// dependency_unavailable: a revoked token or a home server that's off isn't fixed by retrying in
// five seconds. See docs/backend/analysis/mealie-integration.md, Question 8.
public union MealieOutcome<T>(
    MealieOutcome<T>.Success,
    MealieOutcome<T>.NotFound,
    MealieOutcome<T>.Forbidden,
    MealieOutcome<T>.Validation,
    MealieOutcome<T>.NotConnected,
    MealieOutcome<T>.UpstreamFailed)
{
    public sealed record Success(T Value);
    public sealed record NotFound;
    public sealed record Forbidden;
    public sealed record Validation(ValidationProblem Problem);
    public sealed record NotConnected;
    public sealed record UpstreamFailed(MealieFailure Failure);
}

public static class MealieOutcome
{
    public static MealieOutcome<T> Denied<T>(MealplanAccess access) =>
        access == MealplanAccess.Forbidden ? new MealieOutcome<T>.Forbidden() : new MealieOutcome<T>.NotFound();

    // A Mealie 404 for the thing asked for (a deleted recipe or list) is this route's 404.
    public static MealieOutcome<T> FromFailure<T>(MealieFailure failure) =>
        failure.Kind == MealieFailureKind.NotFound ? new MealieOutcome<T>.NotFound() : new MealieOutcome<T>.UpstreamFailed(failure);
}

public static class MealieErrorCodes
{
    public const string NotConnected = "mealie_not_connected";
    public const string Unreachable = "mealie_unreachable";
    public const string TokenRejected = "mealie_token_rejected";
    public const string Error = "mealie_error";

    public static string For(MealieFailureKind kind) => kind switch
    {
        MealieFailureKind.Unreachable => Unreachable,
        MealieFailureKind.TokenRejected => TokenRejected,
        MealieFailureKind.NotFound or MealieFailureKind.Error => Error,
        _ => Error,
    };
}

public static class MealieOutcomeHttp
{
    public static Http.Results<Http.Ok<T>, Http.NotFound, Http.ForbidHttpResult, Http.BadRequest<ErrorEnvelope>, Http.Conflict<ErrorEnvelope>, Http.JsonHttpResult<ErrorEnvelope>> ToHttpResult<T>(
        this MealieOutcome<T> outcome, HttpContext httpContext) => outcome switch
        {
            MealieOutcome<T>.Success(var value) => TypedResults.Ok(value),
            MealieOutcome<T>.NotFound => TypedResults.NotFound(),
            MealieOutcome<T>.Forbidden => TypedResults.Forbid(),
            MealieOutcome<T>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
            MealieOutcome<T>.NotConnected => TypedResults.Conflict(NotConnectedEnvelope(httpContext)),
            MealieOutcome<T>.UpstreamFailed(var failure) => TypedResults.Json(UpstreamEnvelope(failure, httpContext), statusCode: StatusCodes.Status502BadGateway),
        };

    // The same, for a slice whose success has no body (204).
    public static Http.Results<Http.NoContent, Http.NotFound, Http.ForbidHttpResult, Http.BadRequest<ErrorEnvelope>, Http.Conflict<ErrorEnvelope>, Http.JsonHttpResult<ErrorEnvelope>> ToNoContentResult(
        this MealieOutcome<Unit> outcome, HttpContext httpContext) => outcome switch
        {
            MealieOutcome<Unit>.Success => TypedResults.NoContent(),
            MealieOutcome<Unit>.NotFound => TypedResults.NotFound(),
            MealieOutcome<Unit>.Forbidden => TypedResults.Forbid(),
            MealieOutcome<Unit>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
            MealieOutcome<Unit>.NotConnected => TypedResults.Conflict(NotConnectedEnvelope(httpContext)),
            MealieOutcome<Unit>.UpstreamFailed(var failure) => TypedResults.Json(UpstreamEnvelope(failure, httpContext), statusCode: StatusCodes.Status502BadGateway),
        };

    // The 409 and 502 codes every route that calls Mealie can answer with, for the OpenAPI contract.
    public static RouteHandlerBuilder ProducesMealieErrors(this RouteHandlerBuilder builder) => builder
        .ProducesErrorCode(StatusCodes.Status409Conflict, MealieErrorCodes.NotConnected)
        .ProducesErrorCode(StatusCodes.Status502BadGateway, MealieErrorCodes.Unreachable, MealieErrorCodes.TokenRejected, MealieErrorCodes.Error);

    internal static ErrorEnvelope NotConnectedEnvelope(HttpContext httpContext) => new(
        MealieErrorCodes.NotConnected,
        "This family hasn't connected a Mealie server yet.",
        new Dictionary<string, string[]>(),
        httpContext.TraceIdentifier);

    internal static ErrorEnvelope UpstreamEnvelope(MealieFailure failure, HttpContext httpContext) => new(
        MealieErrorCodes.For(failure.Kind),
        failure.Message,
        new Dictionary<string, string[]>(),
        httpContext.TraceIdentifier);
}
