namespace buddy.Common;

// Optional free text -- a meal's description, a rating comment, slot or pickup notes, a goal-post
// label, an AI session's notes -- is a non-null string where "" means "none given". Endpoints
// normalize the optional request field once, here, so a missing, null or whitespace-only value
// all become "" before reaching a command; handler idempotency checks (RateMeal, AssignMealToSlot)
// compare normalized strings, so "" and null can't differ. See
// docs/backend/analysis/eliminate-nulls.md, Phase 4.
public static class FreeText
{
    public static string Normalize(string? value) => value?.Trim() ?? "";
}
