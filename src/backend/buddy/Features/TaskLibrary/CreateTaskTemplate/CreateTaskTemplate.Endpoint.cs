using System.Security.Claims;

using buddy.Common;
using buddy.Features.Calendars;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.TaskLibrary;

public static class CreateTaskTemplateEndpoint
{
    public static RouteGroupBuilder MapCreateTaskTemplate(this RouteGroupBuilder taskTemplates)
    {
        taskTemplates.MapPost("/children/{childId:guid}", async Task<Results<Ok<TaskTemplateResponse>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid childId,
            CreateTaskTemplateRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = CreateTaskTemplate.FromClaims(
                principal,
                new UserId(childId),
                request.Name,
                new Icon(request.Icon),
                new Color(request.Color),
                request.Kind,
                request.EveningBeforeFrom);

            var result = await bus.InvokeAsync<Result<TaskTemplate>>(command, cancellationToken);

            return result switch
            {
                Result<TaskTemplate>.Success(var template) => TypedResults.Ok(TaskTemplateResponse.FromTaskTemplate(template)),
                Result<TaskTemplate>.Forbidden => TypedResults.Forbid(),
                Result<TaskTemplate>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<TaskTemplate>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("CreateTaskTemplate");

        return taskTemplates;
    }
}

// Kind defaults to a routine, so existing clients keep creating routines. EveningBeforeFrom is a
// checklist option (docs/backend/analysis/packing-checklists.md, Question 4); a routine rejects it.
public sealed record CreateTaskTemplateRequest(
    string Name,
    string Icon,
    string Color,
    TaskTemplateKind Kind = TaskTemplateKind.Routine,
    TimeOnly? EveningBeforeFrom = null);

// Weekdays is empty when the item applies whenever the template occurs, otherwise the days it
// applies on (Monday first).
public sealed record SubtaskResponse(Guid Id, string Title, string? Icon, TimeSpan Duration, IReadOnlyList<DayOfWeek> Weekdays);

public sealed record ChecklistOptionsResponse(TimeOnly? EveningBeforeFrom);

// One child suggestion (docs/backend/analysis/packing-checklists.md, Question 8). SubtaskId is the
// item an approval created.
public sealed record ChecklistSuggestionResponse(
    Guid Id,
    string Title,
    IReadOnlyList<DayOfWeek> Weekdays,
    Guid SuggestedBy,
    ChecklistSuggestionStatus Status,
    DateTimeOffset SuggestedAt,
    DateTimeOffset? ResolvedAt,
    Guid? SubtaskId)
{
    public static ChecklistSuggestionResponse From(ChecklistSuggestion suggestion) => new(
        suggestion.Id.Value,
        suggestion.Title,
        suggestion.Weekdays.ToDays(),
        suggestion.SuggestedBy.Value,
        suggestion.Status,
        suggestion.SuggestedAt,
        suggestion.ResolvedAt,
        suggestion.SubtaskId?.Value);
}

// No ChildId -- the caller already knows it (it's the :childId in the request URL), and every
// template returned by a given request belongs to that one child. Subtasks and TotalDuration are
// included so the frontend can render a full template from one list call -- there's no separate
// GetTaskTemplate endpoint in v1. Checklist is null for a routine. Suggestions holds the pending
// ones (oldest first), then the ResolvedSuggestionHistory most recently resolved (newest first);
// the personal data export passes allSuggestions to get every one.
public sealed record TaskTemplateResponse(
    TaskTemplateId Id,
    string Name,
    string Icon,
    string Color,
    IReadOnlyList<SubtaskResponse> Subtasks,
    TimeSpan TotalDuration,
    bool IsArchived,
    Guid CreatedBy,
    Guid LastModifiedBy,
    TaskTemplateKind Kind,
    ChecklistOptionsResponse? Checklist,
    IReadOnlyList<ChecklistSuggestionResponse> Suggestions)
{
    public const int ResolvedSuggestionHistory = 10;

    public static TaskTemplateResponse FromTaskTemplate(TaskTemplate template) => FromTaskTemplate(template, allSuggestions: false);

    public static TaskTemplateResponse FromTaskTemplate(TaskTemplate template, bool allSuggestions) => new(
        template.Id,
        template.Name,
        template.Icon.Value,
        template.Color.Value,
        [.. template.Subtasks.Select(s => new SubtaskResponse(s.Id.Value, s.Title, s.Icon?.Value, s.Duration, s.Weekdays.ToDays()))],
        template.TotalDuration,
        template.IsArchived,
        template.CreatedBy.Value,
        template.LastModifiedBy.Value,
        template.Kind,
        template.Kind == TaskTemplateKind.Checklist ? new ChecklistOptionsResponse(template.Checklist.EveningBeforeFrom) : null,
        [.. SuggestionsToShow(template, allSuggestions).Select(ChecklistSuggestionResponse.From)]);

    private static IEnumerable<ChecklistSuggestion> SuggestionsToShow(TaskTemplate template, bool allSuggestions)
    {
        var resolved = template.Suggestions.Where(s => !s.IsPending).OrderByDescending(s => s.ResolvedAt);

        return template.Suggestions
            .Where(s => s.IsPending)
            .Concat(allSuggestions ? resolved : resolved.Take(ResolvedSuggestionHistory));
    }
}
