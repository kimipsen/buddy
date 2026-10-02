using System.Security.Claims;

using buddy.Common;
using buddy.Features.Calendars;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.TaskLibrary;

public static class UpdateSubtaskEndpoint
{
    public static RouteGroupBuilder MapUpdateSubtask(this RouteGroupBuilder taskTemplates)
    {
        taskTemplates.MapPatch("/{templateId:guid}/subtasks/{subtaskId:guid}", async Task<Results<Ok<TaskTemplateResponse>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid templateId,
            Guid subtaskId,
            UpdateSubtaskRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = UpdateSubtask.FromClaims(
                principal,
                new TaskTemplateId(templateId),
                new SubtaskId(subtaskId),
                request.Title,
                // Blank means "inherit the template's icon", the same as omitting it -- as Calendars does.
                request.Icon is { } icon && !string.IsNullOrWhiteSpace(icon) ? new Icon(icon) : null,
                request.Duration);

            var result = await bus.InvokeAsync<Result<TaskTemplate>>(command, cancellationToken);

            return result switch
            {
                Result<TaskTemplate>.Success(var template) => TypedResults.Ok(TaskTemplateResponse.FromTaskTemplate(template)),
                Result<TaskTemplate>.Forbidden => TypedResults.Forbid(),
                Result<TaskTemplate>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<TaskTemplate>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("UpdateSubtask");

        return taskTemplates;
    }
}

public sealed record UpdateSubtaskRequest(string Title, TimeSpan Duration, string? Icon = null);
