using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Calendars;

public static class ScheduleTaskFromTemplateEndpoint
{
    public static RouteGroupBuilder MapScheduleTaskFromTemplate(this RouteGroupBuilder calendars)
    {
        calendars.MapPost("/{calendarId:guid}/items/from-template", async Task<Results<Ok<CalendarItemResponse>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid calendarId,
            ScheduleTaskFromTemplateRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = ScheduleTaskFromTemplate.FromClaims(
                principal,
                new CalendarId(calendarId),
                request.TaskTemplateId,
                request.StartDate,
                request.StartTime,
                request.Recurrence is { } r ? new RecurrenceRule(r.Frequency, r.IntervalCount, r.Until) : null,
                request.AssignedTo is { } assignedTo ? new UserId(assignedTo) : null,
                request.Title,
                request.Icon is { } icon && !string.IsNullOrWhiteSpace(icon) ? new Icon(icon) : null,
                new Color(request.Color));

            var result = await bus.InvokeAsync<Result<CalendarItem>>(command, cancellationToken);

            return result switch
            {
                Result<CalendarItem>.Success(var item) => TypedResults.Ok(CalendarItemResponse.FromItem(item)),
                Result<CalendarItem>.Forbidden => TypedResults.Forbid(),
                Result<CalendarItem>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<CalendarItem>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("ScheduleTaskFromTemplate");

        return calendars;
    }
}

public sealed record ScheduleTaskFromTemplateRequest(
    Guid TaskTemplateId,
    DateOnly StartDate,
    TimeOnly StartTime,
    RecurrenceRuleRequest? Recurrence,
    Guid? AssignedTo,
    string Title,
    string? Icon,
    string Color);
