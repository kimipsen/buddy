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
        .WithName("CreateTaskTemplate");

        return taskTemplates;
    }
}

public sealed record CreateTaskTemplateRequest(string Name, string Icon, string Color);

public sealed record SubtaskResponse(Guid Id, string Title, string? Icon, TimeSpan Duration);

// No ChildId -- a TaskTemplate is shared by every child in its family (see TaskFamilyResolution),
// so there's no single owning child to report; same contract as MealResponse. Subtasks and
// TotalDuration are included so the frontend can render a full template from one list call --
// there's no separate GetTaskTemplate endpoint in v1.
public sealed record TaskTemplateResponse(
    TaskTemplateId Id,
    string Name,
    string Icon,
    string Color,
    IReadOnlyList<SubtaskResponse> Subtasks,
    TimeSpan TotalDuration,
    bool IsArchived,
    Guid CreatedBy,
    Guid LastModifiedBy)
{
    public static TaskTemplateResponse FromTaskTemplate(TaskTemplate template) => new(
        template.Id,
        template.Name,
        template.Icon.Value,
        template.Color.Value,
        [.. template.Subtasks.Select(s => new SubtaskResponse(s.Id.Value, s.Title, s.Icon?.Value, s.Duration))],
        template.TotalDuration,
        template.IsArchived,
        template.CreatedBy.Value,
        template.LastModifiedBy.Value);
}
