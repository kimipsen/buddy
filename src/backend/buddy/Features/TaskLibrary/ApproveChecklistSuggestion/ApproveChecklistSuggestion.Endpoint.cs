using System.Security.Claims;

using buddy.Common;
using buddy.Common.OpenApi;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.TaskLibrary;

public static class ApproveChecklistSuggestionEndpoint
{
    public static RouteGroupBuilder MapApproveChecklistSuggestion(this RouteGroupBuilder taskTemplates)
    {
        taskTemplates.MapPost("/{templateId:guid}/suggestions/{suggestionId:guid}/approve", async Task<Results<Ok<TaskTemplateResponse>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>, Conflict<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid templateId,
            Guid suggestionId,
            ApproveChecklistSuggestionRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = ApproveChecklistSuggestion.FromClaims(
                principal,
                new TaskTemplateId(templateId),
                new ChecklistSuggestionId(suggestionId),
                request.Title,
                TaskLibraryWeekdays.FromDays(request.Weekdays),
                request.Position);
            var outcome = await bus.InvokeAsync<ChecklistSuggestionOutcome>(command, cancellationToken);

            return outcome.ToHttpResult(httpContext);
        })
        .ProducesErrorCode(StatusCodes.Status409Conflict, ChecklistSuggestionOutcome.AlreadyResolvedCode)
        .WithName("ApproveChecklistSuggestion");

        return taskTemplates;
    }
}

// The guardian's final title and weekdays (null: every day the list occurs); a null Position appends.
public sealed record ApproveChecklistSuggestionRequest(string Title, IReadOnlyList<DayOfWeek>? Weekdays = null, int? Position = null);
