using System.Security.Claims;

using buddy.Common.Validation;
using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record StartAiSession(
    UserId UserId,
    UserId ChildId,
    DateOnly From,
    DateOnly To,
    IReadOnlyCollection<MealSlot> RequestedSlots,
    IReadOnlyCollection<MealId> MustIncludeMealIds,
    string Notes)
{
    public static StartAiSession FromClaims(
        ClaimsPrincipal principal, UserId childId, DateOnly from, DateOnly to,
        IReadOnlyCollection<MealSlot> requestedSlots, IReadOnlyCollection<MealId> mustIncludeMealIds, string notes) =>
        new(principal.GetRequiredUserId(), childId, from, to, requestedSlots, mustIncludeMealIds, notes);
}

// Result<T> plus the one outcome that doesn't fit its four cases: the family hasn't acknowledged what
// the assistant shares (GDPR Question 6.3), rendered as 409 ai_data_sharing_not_acknowledged.
public union StartAiSessionOutcome(
    StartAiSessionOutcome.Success,
    StartAiSessionOutcome.NotFound,
    StartAiSessionOutcome.Forbidden,
    StartAiSessionOutcome.Validation,
    StartAiSessionOutcome.DataSharingNotAcknowledged)
{
    public sealed record Success(AiSessionView View);
    public sealed record NotFound;
    public sealed record Forbidden;
    public sealed record Validation(ValidationProblem Problem);
    public sealed record DataSharingNotAcknowledged;

    public const string DataSharingNotAcknowledgedCode = "ai_data_sharing_not_acknowledged";
}
