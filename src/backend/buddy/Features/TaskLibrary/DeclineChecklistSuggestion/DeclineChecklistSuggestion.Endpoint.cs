using System.Security.Claims;

using buddy.Common;
using buddy.Common.OpenApi;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.TaskLibrary;

public static class DeclineChecklistSuggestionEndpoint
{
    public static RouteGroupBuilder MapDeclineChecklistSuggestion(this RouteGroupBuilder taskTemplates)
    {
        taskTemplates.MapPost("/{templateId:guid}/suggestions/{suggestionId:guid}/decline", async Task<Results<Ok<TaskTemplateResponse>, NotFound, ForbidHttpResult, Conflict<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid templateId,
            Guid suggestionId,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = DeclineChecklistSuggestion.FromClaims(principal, new TaskTemplateId(templateId), new ChecklistSuggestionId(suggestionId));
            var outcome = await bus.InvokeAsync<ChecklistSuggestionOutcome>(command, cancellationToken);

            return outcome.ToHttpResultWithoutBody(httpContext);
        })
        .ProducesErrorCode(StatusCodes.Status409Conflict, ChecklistSuggestionOutcome.AlreadyResolvedCode)
        .WithName("DeclineChecklistSuggestion");

        return taskTemplates;
    }
}
