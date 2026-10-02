using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Calendars;

public static class SetTaskCompletionEndpoint
{
    public static RouteGroupBuilder MapSetTaskCompletion(this RouteGroupBuilder calendars)
    {
        // A plain task completes as a whole...
        calendars.MapPatch("/{calendarId:guid}/items/{itemId:guid}/completion", (
            ClaimsPrincipal principal,
            Guid calendarId,
            Guid itemId,
            SetTaskCompletionRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
            SetAsync(principal, calendarId, itemId, new CompletionTarget.WholeTask(), request, bus, httpContext, cancellationToken))
        .WithName("SetTaskCompletion");

        // ...while a template-scheduled task completes one subtask at a time.
        calendars.MapPatch("/{calendarId:guid}/items/{itemId:guid}/subtasks/{subtaskId:guid}/completion", (
            ClaimsPrincipal principal,
            Guid calendarId,
            Guid itemId,
            Guid subtaskId,
            SetTaskCompletionRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
            SetAsync(principal, calendarId, itemId, new CompletionTarget.Subtask(subtaskId), request, bus, httpContext, cancellationToken))
        .WithName("SetSubtaskCompletion");

        return calendars;
    }

    private static async Task<Results<Ok<TaskCompletionResponse>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> SetAsync(
        ClaimsPrincipal principal,
        Guid calendarId,
        Guid itemId,
        CompletionTarget target,
        SetTaskCompletionRequest request,
        IMessageBus bus,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command = SetTaskCompletion.FromClaims(principal, new CalendarId(calendarId), new CalendarItemId(itemId), request.Date, request.IsCompleted, target);

        var result = await bus.InvokeAsync<Result<CalendarItem>>(command, cancellationToken);

        return result switch
        {
            Result<CalendarItem>.Success(var item) => TypedResults.Ok(new TaskCompletionResponse(
                item.Id, request.Date, item.CompletionLog.Contains(new CompletionKey(request.Date, target)))),
            Result<CalendarItem>.Forbidden => TypedResults.Forbid(),
            Result<CalendarItem>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
            Result<CalendarItem>.NotFound => TypedResults.NotFound(),
        };
    }
}

public sealed record SetTaskCompletionRequest(DateOnly Date, bool IsCompleted);

public sealed record TaskCompletionResponse(CalendarItemId ItemId, DateOnly OccurrenceDate, bool IsCompleted);
