using System.Security.Claims;

using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Mealplans;

// One reviewed row: either an existing meal (MealId) or a meal to create (NewMealName). Rows with
// the same normalized NewMealName share one new meal, so a merge in the review is just "send the
// same name".
public sealed record MealPlanImportEntry(DateOnly Date, MealSlot Slot, MealId? MealId, string NewMealName, string Notes);

public sealed record CommitMealPlanImport(UserId UserId, UserId ChildId, string Format, IReadOnlyList<MealPlanImportEntry> Entries, bool ArchiveSingleUse)
{
    public static CommitMealPlanImport FromClaims(ClaimsPrincipal principal, UserId childId, string format, IReadOnlyList<MealPlanImportEntry> entries, bool archiveSingleUse) =>
        new(principal.GetRequiredUserId(), childId, format, entries, archiveSingleUse);
}

public sealed record CommitMealPlanImportForGroup(UserId UserId, GroupId GroupId, string Format, IReadOnlyList<MealPlanImportEntry> Entries, bool ArchiveSingleUse)
{
    public static CommitMealPlanImportForGroup FromClaims(ClaimsPrincipal principal, GroupId groupId, string format, IReadOnlyList<MealPlanImportEntry> entries, bool archiveSingleUse) =>
        new(principal.GetRequiredUserId(), groupId, format, entries, archiveSingleUse);
}
