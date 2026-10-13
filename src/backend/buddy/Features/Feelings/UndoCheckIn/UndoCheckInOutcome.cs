using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

namespace buddy.Features.Feelings;

// What UndoCheckIn can answer. The closed undo window only happens here, so it is its own union
// rather than a new Result<T> case (the RewardRequestOutcome precedent).
public union UndoCheckInOutcome(
    UndoCheckInOutcome.Success,
    UndoCheckInOutcome.NotFound,
    UndoCheckInOutcome.UndoWindowClosed)
{
    public const string UndoWindowClosedCode = "check_in_undo_window_closed";

    public sealed record Success;
    public sealed record NotFound;
    public sealed record UndoWindowClosed;

    public Results<NoContent, Microsoft.AspNetCore.Http.HttpResults.NotFound, Conflict<ErrorEnvelope>> ToHttpResult(HttpContext httpContext) => this switch
    {
        Success => TypedResults.NoContent(),
        NotFound => TypedResults.NotFound(),
        UndoWindowClosed => TypedResults.Conflict(new ErrorEnvelope(
            UndoWindowClosedCode,
            $"A check-in can only be undone within {FeelingsRules.UndoWindowMinutes} minutes.",
            new Dictionary<string, string[]>(),
            httpContext.TraceIdentifier)),
    };
}
