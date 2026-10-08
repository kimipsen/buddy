using buddy.Features.Groups;

using JasperFx;

namespace buddy.Features.Users;

// How far a guardian got through the first-login setup guide. NotStarted is never stored: it is
// what GET /users/me/onboarding answers when the user has no progress document yet. See
// docs/frontend/analysis/guardian-onboarding.md.
public enum OnboardingStatus
{
    NotStarted,
    Active,
    Deferred,
    Completed
}

// Only what the guide can't derive from the domain itself: whether it is running, the group it set
// up, and whether the guardian chose to skip inviting other adults. Children, calendar, task and meal
// steps are derived from the setup group on load. Version is the optimistic-concurrency revision the
// caller read (0 when there is no document yet).
public sealed record OnboardingProgress(
    UserId UserId,
    OnboardingStatus Status,
    GroupId? SetupGroupId,
    bool InvitationsSkipped,
    int Version)
{
    public static OnboardingProgress NotStarted(UserId userId) => new(userId, OnboardingStatus.NotStarted, null, false, 0);
}

// A plain Marten document in the Users store, keyed by the user's id -- not an event stream. Marten
// tracks Version as a numeric revision (IRevisioned), which is what rejects a stale write.
public sealed class OnboardingProgressDocument : IRevisioned
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; set; }

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public OnboardingStatus Status { get; set; }

    public Guid? SetupGroupId { get; set; }

    public bool InvitationsSkipped { get; set; }

    public int Version { get; set; }

    public OnboardingProgress ToProgress() => new(
        new UserId(Id),
        Status,
        SetupGroupId is { } groupId ? new GroupId(groupId) : null,
        InvitationsSkipped,
        Version);

    public static OnboardingProgressDocument From(OnboardingProgress progress) => new()
    {
        Id = progress.UserId.Value,
        Status = progress.Status,
        SetupGroupId = progress.SetupGroupId?.Value,
        InvitationsSkipped = progress.InvitationsSkipped,
        Version = progress.Version
    };
}
