using buddy.Features.Users;

namespace buddy.Common.Erasure;

// The person being erased. Email is their address as it was before erasure (normalized; null for a
// child, who has none), so invites addressed to them can be found.
public sealed record ErasureSubject(UserId UserId, string? Email);

// One per feature: erases what that feature holds about a person (see
// docs/backend/analysis/gdpr-data-protection.md). Streams that belong only to the person are
// deleted; shared streams are masked. Every operation is idempotent -- UserErasureService reruns an
// erasure that stopped halfway -- and runs after the person is locked out. The cascade decisions
// (orphaned children, group ownership) are UserErasure's; an eraser only erases.
public interface IPersonalDataEraser
{
    // The feature's Marten store. PersonalDataEraserCoverageTests checks that every store has one.
    Type Store { get; }

    // Lower runs first. Groups (which may delete a group) run before Calendars (which then erases
    // that group's calendars).
    int Order => 0;

    // A guardian deleting their account. Their children are already handled: orphaned ones erased,
    // co-guarded ones left to their other guardians.
    Task EraseGuardianAsync(ErasureSubject guardian, CancellationToken cancellationToken);

    // A child being erased. Data the family shares is anchored to one child in its index documents
    // (meals, the meal plan, AI keys and sessions, task templates): the heir is the sibling it passes
    // to, so the family keeps it -- or null when no sibling is left, and it goes with the child.
    Task EraseChildAsync(ErasureSubject child, UserId? heir, CancellationToken cancellationToken);
}
