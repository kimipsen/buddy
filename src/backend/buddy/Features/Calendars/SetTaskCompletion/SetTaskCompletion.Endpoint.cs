using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Calendars;

public static class SetTaskCompletionEndpoint
{
    public static RouteGroupBuilder MapSetTaskCompletion(this RouteGroupBuilder calendars)
    {
        calendars.MapPatch("/{calendarId:guid}/items/{itemId:guid}/completion", async Task<Results<Ok<TaskCompletionResponse>, NotFound, ForbidHttpResult, BadRequest<string>>> (
            ClaimsPrincipal principal,
            Guid calendarId,
            Guid itemId,
            SetTaskCompletionRequest request,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = SetTaskCompletion.FromClaims(
                principal,
                new CalendarId(calendarId),
                new CalendarItemId(itemId),
                request.Date,
                request.IsCompleted);

            var result = await bus.InvokeAsync<Result<CalendarItem>>(command, cancellationToken);

            return result switch
            {
                Result<CalendarItem>.Success(var item) => TypedResults.Ok(new TaskCompletionResponse(
                    item.Id, request.Date, item.CompletionLog.GetValueOrDefault(request.Date, false))),
                Result<CalendarItem>.Forbidden => TypedResults.Forbid(),
                Result<CalendarItem>.Validation(var message) => TypedResults.BadRequest(message),
                Result<CalendarItem>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("SetTaskCompletion");

        return calendars;
    }
}

public sealed record SetTaskCompletionRequest(DateOnly Date, bool IsCompleted);

public sealed record TaskCompletionResponse(CalendarItemId ItemId, DateOnly OccurrenceDate, bool IsCompleted);
