using System.Collections.Immutable;

using buddy.Features.Calendars;
using buddy.Features.Users;

namespace buddy.Features.Progress;

public union ProgressEvent(
    ProgressStarted,
    StarAwarded,
    StarRevoked,
    MilestoneUnlocked,
    GoalPostsConfigured,
    RewardsConfigured,
    RewardRequested,
    RewardRequestApproved,
    RewardRequestDeclined,
    RewardRequestCancelled
)
{
    public static ProgressEvent FromPayload(object payload) => payload switch
    {
        ProgressStarted e => e,
        StarAwarded e => e,
        StarRevoked e => e,
        MilestoneUnlocked e => e,
        GoalPostsConfigured e => e,
        RewardsConfigured e => e,
        RewardRequested e => e,
        RewardRequestApproved e => e,
        RewardRequestDeclined e => e,
        RewardRequestCancelled e => e,
        _ => throw new ArgumentException($"Unknown progress event payload: {payload.GetType().Name}", nameof(payload)),
    };

    public string EventType => this switch
    {
        ProgressStarted => nameof(ProgressStarted),
        StarAwarded => nameof(StarAwarded),
        StarRevoked => nameof(StarRevoked),
        MilestoneUnlocked => nameof(MilestoneUnlocked),
        GoalPostsConfigured => nameof(GoalPostsConfigured),
        RewardsConfigured => nameof(RewardsConfigured),
        RewardRequested => nameof(RewardRequested),
        RewardRequestApproved => nameof(RewardRequestApproved),
        RewardRequestDeclined => nameof(RewardRequestDeclined),
        RewardRequestCancelled => nameof(RewardRequestCancelled),
    };
}

public sealed record ProgressStarted(ProgressId Id, UserId ChildId, DateTimeOffset OccurredAt);

// Mirrors TaskCompletionChanged's own occurrence keying (CalendarItemId + OccurrenceDate + Target)
// so a recurring task's daily instances -- and, for a template-scheduled task, each of its
// independently-completable subtasks -- are awarded independently, not once for the whole item.
public sealed record StarAwarded(ProgressId Id, CalendarItemId SourceItemId, DateOnly OccurrenceDate, DateTimeOffset OccurredAt, CompletionTarget Target);

// Mirrors the child un-completing the same occurrence (TaskCompletionChanged After: false) --
// not a penalty event, the same correction semantics as DoseStatusChanged's After: Pending undo.
public sealed record StarRevoked(ProgressId Id, CalendarItemId SourceItemId, DateOnly OccurrenceDate, DateTimeOffset OccurredAt, CompletionTarget Target);

public sealed record MilestoneUnlocked(ProgressId Id, int Threshold, DateTimeOffset OccurredAt);

// Guardian-authored, full-replace: each save carries the complete ordered list, mirroring
// UpdateCalendarIcon's plain-field-replace shape rather than inventing a partial-update event.
// See docs/backend/analysis/configurable-goal-posts.md.
public sealed record GoalPostsConfigured(ProgressId Id, ImmutableArray<GoalPost> GoalPosts, DateTimeOffset OccurredAt);

// Guardian-authored, full-replace like GoalPostsConfigured: each save carries the whole catalog.
// See docs/backend/analysis/reward-redemption.md.
public sealed record RewardsConfigured(ProgressId Id, ImmutableArray<Reward> Rewards, UserId ConfiguredBy, DateTimeOffset OccurredAt);

// The child asks for a reward. Name, icon and cost are copied from the catalog (see RewardRequest);
// the cost is reserved from the spendable balance until the request is resolved.
public sealed record RewardRequested(ProgressId Id, RewardRequestId RequestId, RewardId RewardId, string Name, string Icon, int Cost, DateTimeOffset OccurredAt);

// A guardian approves: the request's cost moves from reserved to spent.
public sealed record RewardRequestApproved(ProgressId Id, RewardRequestId RequestId, UserId ApprovedBy, DateTimeOffset OccurredAt);

// A guardian declines: the reservation is released.
public sealed record RewardRequestDeclined(ProgressId Id, RewardRequestId RequestId, UserId DeclinedBy, DateTimeOffset OccurredAt);

// The child withdraws their own pending request: the reservation is released.
public sealed record RewardRequestCancelled(ProgressId Id, RewardRequestId RequestId, DateTimeOffset OccurredAt);
