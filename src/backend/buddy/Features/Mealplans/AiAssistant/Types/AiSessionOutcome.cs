using buddy.Common;
using buddy.Common.Validation;

using Http = Microsoft.AspNetCore.Http.HttpResults;

namespace buddy.Features.Mealplans;

// What StartAiSession and SendAiSessionMessage return: Result<T>'s four cases plus the one that
// doesn't fit them, a family that hasn't acknowledged what the assistant shares (GDPR Question 6.3),
// rendered as 409 ai_data_sharing_not_acknowledged. Both commands send the family's data to the
// provider, so both refuse until it exists.
public union AiSessionOutcome(
    AiSessionOutcome.Success,
    AiSessionOutcome.NotFound,
    AiSessionOutcome.Forbidden,
    AiSessionOutcome.Validation,
    AiSessionOutcome.DataSharingNotAcknowledged)
{
    public sealed record Success(AiSessionView View);
    public sealed record NotFound;
    public sealed record Forbidden;
    public sealed record Validation(ValidationProblem Problem);
    public sealed record DataSharingNotAcknowledged;

    public const string DataSharingNotAcknowledgedCode = "ai_data_sharing_not_acknowledged";

    public static AiSessionOutcome Denied(MealplanAccess access) =>
        access == MealplanAccess.Forbidden ? new Forbidden() : new NotFound();
}

public static class AiSessionOutcomeHttp
{
    public static Http.Results<Http.Ok<AiSessionView>, Http.NotFound, Http.ForbidHttpResult, Http.BadRequest<ErrorEnvelope>, Http.Conflict<ErrorEnvelope>> ToHttpResult(
        this AiSessionOutcome outcome, HttpContext httpContext) => outcome switch
        {
            AiSessionOutcome.Success(var view) => TypedResults.Ok(view),
            AiSessionOutcome.Forbidden => TypedResults.Forbid(),
            AiSessionOutcome.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
            AiSessionOutcome.NotFound => TypedResults.NotFound(),
            AiSessionOutcome.DataSharingNotAcknowledged => TypedResults.Conflict(new ErrorEnvelope(
                AiSessionOutcome.DataSharingNotAcknowledgedCode,
                "A guardian has to acknowledge what the assistant shares with the AI provider before the family uses it.",
                new Dictionary<string, string[]>(),
                httpContext.TraceIdentifier)),
        };
}
