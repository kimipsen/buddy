using System.Security.Claims;

using buddy.Common;
using buddy.Features.Calendars;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.TaskLibrary;

public static class AddSubtaskEndpoint
{
    public static RouteGroupBuilder MapAddSubtask(this RouteGroupBuilder taskTemplates)
    {
        taskTemplates.MapPost("/{templateId:guid}/subtasks", async Task<Results<Ok<TaskTemplateResponse>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid templateId,
            AddSubtaskRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = AddSubtask.FromClaims(
                principal,
                new TaskTemplateId(templateId),
                request.Title,
                request.Icon is null ? null : new Icon(request.Icon),
                request.Duration,
                request.Position);

            var result = await bus.InvokeAsync<Result<TaskTemplate>>(command, cancellationToken);

            return result switch
            {
                Result<TaskTemplate>.Success(var template) => TypedResults.Ok(TaskTemplateResponse.FromTaskTemplate(template)),
                Result<TaskTemplate>.Forbidden => TypedResults.Forbid(),
                Result<TaskTemplate>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<TaskTemplate>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("AddSubtask");

        return taskTemplates;
    }
}

public sealed record AddSubtaskRequest(string Title, string? Icon, TimeSpan Duration, int? Position);
