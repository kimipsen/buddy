using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.SleepDiaries;

public sealed record UpdateSleepHygieneNotes(UserId UserId, UserId ChildId, string Notes)
{
    public static UpdateSleepHygieneNotes FromClaims(ClaimsPrincipal principal, UserId childId, string notes) =>
        new(principal.GetRequiredUserId(), childId, notes);
}
