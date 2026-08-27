using System.Security.Claims;

using buddy.Common;
using buddy.Features.Calendars;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.TaskLibrary;

public static class UpdateTaskTemplateEndpoint
{
    public static RouteGroupBuilder MapUpdateTaskTemplate(this RouteGroupBuilder taskTemplates)
    {
        taskTemplates.MapPatch("/{templateId:guid}", async Task<Results<Ok<TaskTemplateResponse>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid templateId,
            UpdateTaskTemplateRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = UpdateTaskTemplate.FromClaims(
                principal,
                new TaskTemplateId(templateId),
                request.Name,
                new Icon(request.Icon),
                new Color(request.Color));

            var result = await bus.InvokeAsync<Result<TaskTemplate>>(command, cancellationToken);

            return result switch
            {
                Result<TaskTemplate>.Success(var template) => TypedResults.Ok(TaskTemplateResponse.FromTaskTemplate(template)),
                Result<TaskTemplate>.Forbidden => TypedResults.Forbid(),
                Result<TaskTemplate>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<TaskTemplate>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("UpdateTaskTemplate");

        return taskTemplates;
    }
}

public sealed record UpdateTaskTemplateRequest(string Name, string Icon, string Color);
