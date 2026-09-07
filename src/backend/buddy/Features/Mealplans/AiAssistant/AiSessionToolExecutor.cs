using System.Text.Json;

using buddy.Features.Calendars;
using buddy.Features.Groups;
using buddy.Features.Guardians;
using buddy.Features.TaskLibrary;
using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public static class AiSessionToolExecutor
{
    // DraftEvent is null exactly when IsError is true, or when the tool is read-only (there's
    // nothing to persist against the session stream in either case).
    public sealed record ExecutionOutcome(string ResultJson, bool IsError, MealplanAiSessionEvent? DraftEvent);

    public static async Task<ExecutionOutcome> ExecuteAsync(
        AiRequestedToolCall call,
        MealplanAiSessionId sessionId,
        MealplanAiSession session,
        IReadOnlyCollection<MealId> familyMealIds,
        UserId callerId,
        ICalendarEventStore calendars,
        ICalendarItemEventStore calendarItems,
        ITaskTemplateEventStore taskTemplates,
        IGroupEventStore groups,
        IGuardianLinkEventStore guardians,
        DateTimeOffset now,
        CancellationToken cancellationToken) => call.ToolName switch
        {
            AiSessionTools.ProposeAssignment => ExecuteProposeAssignment(call, sessionId, session, familyMealIds, now),
            AiSessionTools.ClearDraftAssignment => ExecuteClearDraftAssignment(call, sessionId, now),
            AiSessionTools.GetCalendarConflicts => await ExecuteGetCalendarConflictsAsync(
                call, session, callerId, calendars, calendarItems, taskTemplates, groups, guardians, cancellationToken),
            _ => new ExecutionOutcome(Error($"Unknown tool: {call.ToolName}"), true, null),
        };

    private static ExecutionOutcome ExecuteProposeAssignment(
        AiRequestedToolCall call, MealplanAiSessionId sessionId, MealplanAiSession session, IReadOnlyCollection<MealId> familyMealIds, DateTimeOffset now)
    {
        if (!TryParseArguments(call.ArgumentsJson, out var args, out var parseError))
        {
            return new ExecutionOutcome(Error(parseError), true, null);
        }

        if (!TryGetDate(args, "date", out var date, out var dateError))
        {
            return new ExecutionOutcome(Error(dateError), true, null);
        }

        if (!TryGetSlot(args, out var slot, out var slotError))
        {
            return new ExecutionOutcome(Error(slotError), true, null);
        }

        if (!args.TryGetProperty("meal_id", out var mealIdElement) || !Guid.TryParse(mealIdElement.GetString(), out var mealGuid))
        {
            return new ExecutionOutcome(Error("\"meal_id\" must be a valid meal id from the available meal library."), true, null);
        }

        var mealId = new MealId(mealGuid);

        if (date < session.From || date > session.To)
        {
            return new ExecutionOutcome(Error($"{date:yyyy-MM-dd} is outside the requested range {session.From:yyyy-MM-dd} to {session.To:yyyy-MM-dd}."), true, null);
        }

        if (!session.RequestedSlots.Contains(slot))
        {
            return new ExecutionOutcome(Error($"{slot} was not one of the requested meal slots for this session."), true, null);
        }

        if (!familyMealIds.Contains(mealId))
        {
            return new ExecutionOutcome(Error("That meal id is not in the family's available meal library."), true, null);
        }

        return new ExecutionOutcome(Ok(), false, new AiDraftAssignmentSet(sessionId, date, slot, mealId, now));
    }

    private static ExecutionOutcome ExecuteClearDraftAssignment(AiRequestedToolCall call, MealplanAiSessionId sessionId, DateTimeOffset now)
    {
        if (!TryParseArguments(call.ArgumentsJson, out var args, out var parseError))
        {
            return new ExecutionOutcome(Error(parseError), true, null);
        }

        if (!TryGetDate(args, "date", out var date, out var dateError))
        {
            return new ExecutionOutcome(Error(dateError), true, null);
        }

        if (!TryGetSlot(args, out var slot, out var slotError))
        {
            return new ExecutionOutcome(Error(slotError), true, null);
        }

        return new ExecutionOutcome(Ok(), false, new AiDraftAssignmentCleared(sessionId, date, slot, now));
    }

    private static async Task<ExecutionOutcome> ExecuteGetCalendarConflictsAsync(
        AiRequestedToolCall call, MealplanAiSession session, UserId callerId,
        ICalendarEventStore calendars, ICalendarItemEventStore calendarItems, ITaskTemplateEventStore taskTemplates,
        IGroupEventStore groups, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        if (!TryParseArguments(call.ArgumentsJson, out var args, out var parseError))
        {
            return new ExecutionOutcome(Error(parseError), true, null);
        }

        if (!TryGetDate(args, "from", out var from, out var fromError))
        {
            return new ExecutionOutcome(Error(fromError), true, null);
        }

        if (!TryGetDate(args, "to", out var to, out var toError))
        {
            return new ExecutionOutcome(Error(toError), true, null);
        }

        if (to < from || from < session.From || to > session.To)
        {
            return new ExecutionOutcome(
                Error($"The range must be within the session's requested dates ({session.From:yyyy-MM-dd} to {session.To:yyyy-MM-dd})."), true, null);
        }

        var occurrences = await CalendarConflictLookup.FindOccurrencesAsync(callerId, from, to, calendars, calendarItems, taskTemplates, groups, guardians, cancellationToken);

        var events = occurrences.Select(o => new
        {
            date = (o.StartsAt ?? o.DueAt)!.Value.ToString("yyyy-MM-dd"),
            title = o.Title,
            allDay = o.IsAllDay
        });

        return new ExecutionOutcome(JsonSerializer.Serialize(new { status = "ok", events }), false, null);
    }

    private static bool TryParseArguments(string argumentsJson, out JsonElement args, out string error)
    {
        try
        {
            using var document = JsonDocument.Parse(argumentsJson);
            args = document.RootElement.Clone();
            error = "";
            return true;
        }
        catch (JsonException)
        {
            args = default;
            error = "Tool arguments were not valid JSON.";
            return false;
        }
    }

    private static bool TryGetDate(JsonElement args, string propertyName, out DateOnly date, out string error)
    {
        if (args.TryGetProperty(propertyName, out var dateElement) && DateOnly.TryParse(dateElement.GetString(), out date))
        {
            error = "";
            return true;
        }

        date = default;
        error = $"\"{propertyName}\" must be an ISO-8601 date (YYYY-MM-DD).";
        return false;
    }

    private static bool TryGetSlot(JsonElement args, out MealSlot slot, out string error)
    {
        if (args.TryGetProperty("slot", out var slotElement) && Enum.TryParse(slotElement.GetString(), ignoreCase: true, out slot))
        {
            error = "";
            return true;
        }

        slot = default;
        error = "\"slot\" must be one of Breakfast, Lunch, Dinner, Snack.";
        return false;
    }

    private static string Ok() => """{"status":"ok"}""";

    private static string Error(string message) => JsonSerializer.Serialize(new { status = "error", message });
}
