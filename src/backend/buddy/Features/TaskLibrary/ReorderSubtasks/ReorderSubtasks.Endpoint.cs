using System.Collections.Immutable;
using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.TaskLibrary;

public static class ReorderSubtasksEndpoint
{
    public static RouteGroupBuilder MapReorderSubtasks(this RouteGroupBuilder taskTemplates)
    {
        taskTemplates.MapPut("/{templateId:guid}/subtasks/order", async Task<Results<Ok<TaskTemplateResponse>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid templateId,
            ReorderSubtasksRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = ReorderSubtasks.FromClaims(
                principal,
                new TaskTemplateId(templateId),
                [.. request.NewOrder.Select(id => new SubtaskId(id))]);

            var result = await bus.InvokeAsync<Result<TaskTemplate>>(command, cancellationToken);

            return result switch
            {
                Result<TaskTemplate>.Success(var template) => TypedResults.Ok(TaskTemplateResponse.FromTaskTemplate(template)),
                Result<TaskTemplate>.Forbidden => TypedResults.Forbid(),
                Result<TaskTemplate>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<TaskTemplate>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("ReorderSubtasks");

        return taskTemplates;
    }
}

public sealed record ReorderSubtasksRequest(ImmutableList<Guid> NewOrder);
