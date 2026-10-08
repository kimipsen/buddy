using System.Security.Claims;

using buddy.Features.Groups;

namespace buddy.Features.Users;

// ExpectedVersion is the revision the caller last read (0 when GET answered NotStarted).
public sealed record UpdateOnboardingProgress(
    UserId UserId,
    OnboardingStatus Status,
    GroupId? SetupGroupId,
    bool InvitationsSkipped,
    int ExpectedVersion)
{
    public static UpdateOnboardingProgress FromClaims(
        ClaimsPrincipal principal,
        OnboardingStatus status,
        GroupId? setupGroupId,
        bool invitationsSkipped,
        int expectedVersion) =>
        new(principal.GetRequiredUserId(), status, setupGroupId, invitationsSkipped, expectedVersion);
}
