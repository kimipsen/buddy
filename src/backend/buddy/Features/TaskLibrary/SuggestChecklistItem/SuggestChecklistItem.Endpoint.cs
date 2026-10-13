using System.Security.Claims;

using buddy.Common;
using buddy.Common.OpenApi;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.TaskLibrary;

public static class SuggestChecklistItemEndpoint
{
    public static RouteGroupBuilder MapSuggestChecklistItem(this RouteGroupBuilder taskTemplates)
    {
        taskTemplates.MapPost("/{templateId:guid}/suggestions", async Task<Results<Ok<TaskTemplateResponse>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>, Conflict<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid templateId,
            SuggestChecklistItemRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = SuggestChecklistItem.FromClaims(
                principal, new TaskTemplateId(templateId), request.Title, TaskLibraryWeekdays.FromDays(request.Weekdays));
            var outcome = await bus.InvokeAsync<ChecklistSuggestionOutcome>(command, cancellationToken);

            return outcome.ToHttpResult(httpContext);
        })
        .ProducesErrorCode(StatusCodes.Status409Conflict, ChecklistSuggestionOutcome.TooManyPendingCode)
        .ProducesErrorCode(StatusCodes.Status409Conflict, ChecklistSuggestionOutcome.ItemExistsCode)
        .WithName("SuggestChecklistItem");

        return taskTemplates;
    }
}

// Weekdays is the child's proposal: null for "whenever the list occurs", else the days.
public sealed record SuggestChecklistItemRequest(string Title, IReadOnlyList<DayOfWeek>? Weekdays = null);
