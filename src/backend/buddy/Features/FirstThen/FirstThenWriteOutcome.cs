using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

namespace buddy.Features.FirstThen;

// A Result<FirstThenBoardResponse> plus BoardChanged: the board was replaced (or a write named a
// revision that isn't the current one) since the caller loaded it, so the client must reload and
// show the current board first. AcknowledgeRule's RevisionChanged, copied
// (first-then-board.md, Question 5).
public union FirstThenWriteOutcome(
    FirstThenWriteOutcome.Success,
    FirstThenWriteOutcome.NotFound,
    FirstThenWriteOutcome.Forbidden,
    FirstThenWriteOutcome.Validation,
    FirstThenWriteOutcome.BoardChanged)
{
    public const string BoardChangedCode = "first_then_board_changed";

    public sealed record Success(FirstThenBoardResponse Board);
    public sealed record NotFound;
    public sealed record Forbidden;
    public sealed record Validation(buddy.Common.Validation.ValidationProblem Problem);
    public sealed record BoardChanged(int CurrentRevision);
}

public static class FirstThenWriteOutcomeExtensions
{
    public static Results<Ok<FirstThenBoardResponse>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>, Conflict<ErrorEnvelope>> ToHttpResult(
        this FirstThenWriteOutcome outcome,
        HttpContext httpContext) => outcome switch
        {
            FirstThenWriteOutcome.Success(var board) => TypedResults.Ok(board),
            FirstThenWriteOutcome.NotFound => TypedResults.NotFound(),
            FirstThenWriteOutcome.Forbidden => TypedResults.Forbid(),
            FirstThenWriteOutcome.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
            FirstThenWriteOutcome.BoardChanged(var current) => TypedResults.Conflict(new ErrorEnvelope(
                FirstThenWriteOutcome.BoardChangedCode,
                $"The board has changed since it was loaded (now revision {current}). Reload it first.",
                new Dictionary<string, string[]>(),
                httpContext.TraceIdentifier)),
        };
}
