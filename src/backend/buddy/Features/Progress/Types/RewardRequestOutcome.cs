using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

namespace buddy.Features.Progress;

// What the four request slices (request, cancel, approve, decline) can answer. The three conflicts
// only happen here, so they live in their own union rather than as new Result<T> cases (see
// Result.cs). See docs/backend/analysis/reward-redemption.md, Question 3.
public union RewardRequestOutcome(
    RewardRequestOutcome.Success,
    RewardRequestOutcome.NotFound,
    RewardRequestOutcome.Forbidden,
    RewardRequestOutcome.InsufficientStars,
    RewardRequestOutcome.TooManyPendingRequests,
    RewardRequestOutcome.AlreadyResolved)
{
    public const string InsufficientStarsCode = "insufficient_stars";
    public const string TooManyPendingRequestsCode = "too_many_pending_requests";
    public const string AlreadyResolvedCode = "reward_request_resolved";

    public sealed record Success(ProgressSummary Summary);
    public sealed record NotFound;
    public sealed record Forbidden;
    public sealed record InsufficientStars;
    public sealed record TooManyPendingRequests;
    public sealed record AlreadyResolved;

    public Results<Ok<ProgressSummary>, Microsoft.AspNetCore.Http.HttpResults.NotFound, ForbidHttpResult, Conflict<ErrorEnvelope>> ToHttpResult(HttpContext httpContext) => this switch
    {
        Success(var summary) => TypedResults.Ok(summary),
        NotFound => TypedResults.NotFound(),
        Forbidden => TypedResults.Forbid(),
        InsufficientStars => Conflict(httpContext, InsufficientStarsCode, "The child doesn't have enough stars for this reward."),
        TooManyPendingRequests => Conflict(httpContext, TooManyPendingRequestsCode, $"At most {RewardRequestRules.MaxPendingRequests} reward requests can wait for a guardian at once."),
        AlreadyResolved => Conflict(httpContext, AlreadyResolvedCode, "The reward request has already been resolved differently."),
    };

    private static Conflict<ErrorEnvelope> Conflict(HttpContext httpContext, string code, string message) =>
        TypedResults.Conflict(new ErrorEnvelope(code, message, new Dictionary<string, string[]>(), httpContext.TraceIdentifier));
}

public static class RewardRequestRules
{
    public const int MaxPendingRequests = 10;
}
