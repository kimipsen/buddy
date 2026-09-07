namespace buddy.Features.Mealplans;

// propose_assignment/clear_draft_assignment are the model's only way to affect state -- see
// AiSessionToolExecutor, which validates every call against the session's requested range/slots
// and the family's actual meal library before ever touching the draft. get_calendar_conflicts is
// read-only: it lets the model check the family's calendar before proposing meals for a date.
public static class AiSessionTools
{
    public const string ProposeAssignment = "propose_assignment";
    public const string ClearDraftAssignment = "clear_draft_assignment";
    public const string GetCalendarConflicts = "get_calendar_conflicts";

    public static IReadOnlyList<AiToolDefinition> Definitions { get; } =
    [
        new AiToolDefinition(
            ProposeAssignment,
            "Assign a meal from the family's available meal library to a date and slot in the draft plan. Only call this for dates within the requested range and slots the guardian asked for.",
            """
            {
              "type": "object",
              "properties": {
                "date": { "type": "string", "description": "ISO-8601 date (YYYY-MM-DD)." },
                "slot": { "type": "string", "enum": ["Breakfast", "Lunch", "Dinner", "Snack"] },
                "meal_id": { "type": "string", "description": "Id of a meal from the available meal library." },
                "rationale": { "type": "string", "description": "A short, family-facing reason for this pick." }
              },
              "required": ["date", "slot", "meal_id", "rationale"]
            }
            """),
        new AiToolDefinition(
            ClearDraftAssignment,
            "Remove a previously proposed assignment from the draft plan for a date and slot.",
            """
            {
              "type": "object",
              "properties": {
                "date": { "type": "string", "description": "ISO-8601 date (YYYY-MM-DD)." },
                "slot": { "type": "string", "enum": ["Breakfast", "Lunch", "Dinner", "Snack"] }
              },
              "required": ["date", "slot"]
            }
            """),
        new AiToolDefinition(
            GetCalendarConflicts,
            "Look up the family's calendar events in a date range (must be within the session's requested range) before proposing meals for those dates, so you can flag things like dining out or a trip instead of silently overwriting them.",
            """
            {
              "type": "object",
              "properties": {
                "from": { "type": "string", "description": "ISO-8601 date (YYYY-MM-DD)." },
                "to": { "type": "string", "description": "ISO-8601 date (YYYY-MM-DD)." }
              },
              "required": ["from", "to"]
            }
            """)
    ];
}
