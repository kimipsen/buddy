using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Guardians;

// A guardian sets the child's homework focus and break lengths on the child's own User stream --
// same shape as UpdateChildVisualTimers (docs/frontend/analysis/homework-splitter.md, Question 2).
public sealed record UpdateChildHomeworkChunks(UserId GuardianId, UserId ChildId, HomeworkChunks HomeworkChunks)
{
    public static UpdateChildHomeworkChunks FromClaims(ClaimsPrincipal principal, UserId childId, HomeworkChunks homeworkChunks) =>
        new(principal.GetRequiredUserId(), childId, homeworkChunks);
}
