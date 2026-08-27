using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.TaskLibrary;

public static class RemoveSubtaskEndpoint
{
    public static RouteGroupBuilder MapRemoveSubtask(this RouteGroupBuilder taskTemplates)
    {
        taskTemplates.MapDelete("/{templateId:guid}/subtasks/{subtaskId:guid}", async Task<Results<NoContent, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid templateId,
            Guid subtaskId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = RemoveSubtask.FromClaims(principal, new TaskTemplateId(templateId), new SubtaskId(subtaskId));
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.Forbidden => TypedResults.Forbid(),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                // RemoveSubtaskHandler never produces Validation -- there's no BadRequest in this
                // route's declared results, so this collapses to NotFound like the others (same
                // convention ArchiveMealEndpoint uses).
                Result<Unit>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("RemoveSubtask");

        return taskTemplates;
    }
}
