using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

namespace buddy.Features.Feelings;

// What EditFeelingCheckIn can answer. "That's the child's own words" only happens here, so it is
// its own union rather than a new Result<T> case (the RewardRequestOutcome precedent).
public union EditFeelingCheckInOutcome(
    EditFeelingCheckInOutcome.Success,
    EditFeelingCheckInOutcome.NotFound,
    EditFeelingCheckInOutcome.Forbidden,
    EditFeelingCheckInOutcome.Validation,
    EditFeelingCheckInOutcome.IsSelfReport)
{
    public const string IsSelfReportCode = "check_in_is_self_report";

    public sealed record Success(FeelingCheckInResponse CheckIn);
    public sealed record NotFound;
    public sealed record Forbidden;
    public sealed record Validation(buddy.Common.Validation.ValidationProblem Problem);
    public sealed record IsSelfReport;

    public static EditFeelingCheckInOutcome Denied(FeelingsAccess access) => access.ToDeniedResult<Unit>() switch
    {
        Result<Unit>.Forbidden => new Forbidden(),
        Result<Unit>.NotFound or Result<Unit>.Success or Result<Unit>.Validation => new NotFound(),
    };

    public Results<Ok<FeelingCheckInResponse>, Microsoft.AspNetCore.Http.HttpResults.NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>, Conflict<ErrorEnvelope>> ToHttpResult(HttpContext httpContext) => this switch
    {
        Success(var checkIn) => TypedResults.Ok(checkIn),
        NotFound => TypedResults.NotFound(),
        Forbidden => TypedResults.Forbid(),
        Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
        IsSelfReport => TypedResults.Conflict(new ErrorEnvelope(
            IsSelfReportCode,
            "This is the child's own check-in. It can be removed, but not edited.",
            new Dictionary<string, string[]>(),
            httpContext.TraceIdentifier)),
    };
}
