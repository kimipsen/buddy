using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.TaskLibrary;

public static class SetChecklistOptionsEndpoint
{
    public static RouteGroupBuilder MapSetChecklistOptions(this RouteGroupBuilder taskTemplates)
    {
        taskTemplates.MapPut("/{templateId:guid}/checklist-options", async Task<Results<Ok<TaskTemplateResponse>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid templateId,
            SetChecklistOptionsRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = SetChecklistOptions.FromClaims(principal, new TaskTemplateId(templateId), request.EveningBeforeFrom);
            var result = await bus.InvokeAsync<Result<TaskTemplate>>(command, cancellationToken);

            return result switch
            {
                Result<TaskTemplate>.Success(var template) => TypedResults.Ok(TaskTemplateResponse.FromTaskTemplate(template)),
                Result<TaskTemplate>.Forbidden => TypedResults.Forbid(),
                Result<TaskTemplate>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<TaskTemplate>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("SetChecklistOptions");

        return taskTemplates;
    }
}

// Null shows the packing list on the day only; a time shows tomorrow's list from then.
public sealed record SetChecklistOptionsRequest(TimeOnly? EveningBeforeFrom);
