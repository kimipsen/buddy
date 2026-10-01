using Microsoft.AspNetCore.Http.HttpResults;

namespace buddy.Common.RateLimiting;

// The one resend cooldown shared by every "send that email again" command: InviteGuardian,
// InviteToGroup and ResendEmailVerification. Kept as plain state-dependent logic rather than a
// FluentValidation rule: it needs a store read (the existing pending invite's CreatedAt, the
// user's last verification request) the handler already does, and runs after authorization on
// purpose -- the same reason AssignPickup's relationship checks aren't converted either.
//
// An active cooldown is 409 Conflict with the `resend_cooldown` ErrorEnvelope code, per
// docs/backend/http-status-codes.md ("resend email verification during cooldown window"): the
// request is valid, it conflicts with the state "an email was sent less than Window ago".
// Handlers return ResendCooldownActive as a case of their feature-specific outcome union (it
// doesn't fit Result<T>'s four cases); endpoints render it with ToConflict.
public static class ResendCooldown
{
    public const string ErrorCode = "resend_cooldown";

    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public static bool IsActive(DateTimeOffset? lastSentAt, DateTimeOffset now) =>
        lastSentAt is { } sentAt && now - sentAt < Window;

    public static Conflict<ErrorEnvelope> ToConflict(this ResendCooldownActive cooldown, HttpContext context) =>
        TypedResults.Conflict(new ErrorEnvelope(ErrorCode, cooldown.Message, new Dictionary<string, string[]>(), context.TraceIdentifier));
}

public sealed record ResendCooldownActive(string Message);
