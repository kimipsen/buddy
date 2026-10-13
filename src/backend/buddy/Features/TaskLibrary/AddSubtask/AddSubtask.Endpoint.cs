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
                // Blank means "inherit the template's icon", the same as omitting it -- as Calendars does.
                request.Icon is { } icon && !string.IsNullOrWhiteSpace(icon) ? new Icon(icon) : null,
                request.Duration ?? TimeSpan.Zero,
                request.Position,
                TaskLibraryWeekdays.FromDays(request.Weekdays));

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

// Duration is required for a routine step and must be zero (or left out) for a checklist item.
// Weekdays (null for "whenever the template occurs") is offered for checklist items in the UI.
public sealed record AddSubtaskRequest(string Title, TimeSpan? Duration = null, string? Icon = null, int? Position = null, IReadOnlyList<DayOfWeek>? Weekdays = null);
