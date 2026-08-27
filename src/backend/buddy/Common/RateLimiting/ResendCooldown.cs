namespace buddy.Common.RateLimiting;

// Dedupes the resend-cooldown constant + check InviteToGroupHandler and InviteGuardianHandler
// each defined for themselves. Kept as plain state-dependent logic rather than a FluentValidation
// rule: it needs a store read (the existing pending invite's CreatedAt) the handler already does,
// and runs after authorization on purpose -- the same reason AssignPickup's relationship checks
// aren't converted either.
public static class ResendCooldown
{
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public static bool IsActive(DateTimeOffset? lastSentAt, DateTimeOffset now) =>
        lastSentAt is { } sentAt && now - sentAt < Window;
}
