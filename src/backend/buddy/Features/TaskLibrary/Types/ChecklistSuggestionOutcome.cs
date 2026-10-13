using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

namespace buddy.Features.TaskLibrary;

// What the four suggestion slices (suggest, withdraw, approve, decline) can answer. The three
// conflicts only happen here, so they live in their own union rather than as new Result<T> cases,
// as RewardRequestOutcome does (docs/backend/analysis/packing-checklists.md, Question 8).
public union ChecklistSuggestionOutcome(
    ChecklistSuggestionOutcome.Success,
    ChecklistSuggestionOutcome.NotFound,
    ChecklistSuggestionOutcome.Forbidden,
    ChecklistSuggestionOutcome.Validation,
    ChecklistSuggestionOutcome.TooManyPending,
    ChecklistSuggestionOutcome.ItemExists,
    ChecklistSuggestionOutcome.AlreadyResolved)
{
    public const string TooManyPendingCode = "too_many_pending_suggestions";
    public const string ItemExistsCode = "checklist_item_exists";
    public const string AlreadyResolvedCode = "suggestion_resolved";

    public sealed record Success(TaskTemplate Template);
    public sealed record NotFound;
    public sealed record Forbidden;
    public sealed record Validation(buddy.Common.Validation.ValidationProblem Problem);
    public sealed record TooManyPending;
    public sealed record ItemExists;
    public sealed record AlreadyResolved;

    // The authorization preamble's failure, for a slice that resolved the template through
    // TaskTemplateAccess.
    internal static ChecklistSuggestionOutcome Denied(Result<ResolvedTaskTemplate> resolved) => resolved switch
    {
        Result<ResolvedTaskTemplate>.Forbidden => new Forbidden(),
        Result<ResolvedTaskTemplate>.Validation(var problem) => new Validation(problem),
        Result<ResolvedTaskTemplate>.NotFound or Result<ResolvedTaskTemplate>.Success => new NotFound(),
    };

    public Results<Ok<TaskTemplateResponse>, Microsoft.AspNetCore.Http.HttpResults.NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>, Conflict<ErrorEnvelope>> ToHttpResult(HttpContext httpContext) => this switch
    {
        Success(var template) => TypedResults.Ok(TaskTemplateResponse.FromTaskTemplate(template)),
        NotFound => TypedResults.NotFound(),
        Forbidden => TypedResults.Forbid(),
        Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
        TooManyPending => Conflict(httpContext, TooManyPendingCode, $"At most {ChecklistSuggestionRules.MaxPending} suggestions can wait for a guardian on one list."),
        ItemExists => Conflict(httpContext, ItemExistsCode, "That item is already on the list."),
        AlreadyResolved => Conflict(httpContext, AlreadyResolvedCode, "The suggestion has already been resolved differently."),
    };

    // Withdraw and decline take no body, so they never answer 400: their handlers never return
    // Validation, which collapses to NotFound here (same convention as RemoveSubtaskEndpoint).
    public Results<Ok<TaskTemplateResponse>, Microsoft.AspNetCore.Http.HttpResults.NotFound, ForbidHttpResult, Conflict<ErrorEnvelope>> ToHttpResultWithoutBody(HttpContext httpContext) => this switch
    {
        Success(var template) => TypedResults.Ok(TaskTemplateResponse.FromTaskTemplate(template)),
        NotFound or Validation => TypedResults.NotFound(),
        Forbidden => TypedResults.Forbid(),
        TooManyPending => Conflict(httpContext, TooManyPendingCode, $"At most {ChecklistSuggestionRules.MaxPending} suggestions can wait for a guardian on one list."),
        ItemExists => Conflict(httpContext, ItemExistsCode, "That item is already on the list."),
        AlreadyResolved => Conflict(httpContext, AlreadyResolvedCode, "The suggestion has already been resolved differently."),
    };

    private static Conflict<ErrorEnvelope> Conflict(HttpContext httpContext, string code, string message) =>
        TypedResults.Conflict(new ErrorEnvelope(code, message, new Dictionary<string, string[]>(), httpContext.TraceIdentifier));
}

public static class ChecklistSuggestionRules
{
    public const int MaxPending = 5;

    // Suggestion titles are compared trimmed (the command already trims them) and ignoring case.
    public static bool SameTitle(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
}
