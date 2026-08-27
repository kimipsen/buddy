using buddy.Features.Users;

namespace buddy.Features.Guardians;

public interface IGuardianLinkEventStore
{
    Task<IReadOnlyCollection<GuardianEvent>> ReadAsync(GuardianLinkId id, CancellationToken cancellationToken);

    Task AppendAsync(GuardianLinkId id, IReadOnlyCollection<GuardianEvent> events, CancellationToken cancellationToken);

    Task<GuardianLinkDocument?> FindActiveLinkAsync(UserId childId, UserId guardianId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<GuardianLinkDocument>> ListForGuardianAsync(UserId guardianId, CancellationToken cancellationToken);

    // Answers "is this caller a child" -- a non-empty, non-revoked result means the account is a
    // child linked to at least one guardian, which the frontend uses to pick guardian vs child UI.
    Task<IReadOnlyCollection<GuardianLinkDocument>> ListForChildAsync(UserId childId, CancellationToken cancellationToken);

    // Bulk variant of ListForChildAsync for "which of these UserIds are children" checks (e.g.
    // tagging group members as child vs guardian) -- one IN query instead of N single lookups.
    Task<IReadOnlyCollection<UserId>> FilterChildrenAsync(IReadOnlyCollection<UserId> userIds, CancellationToken cancellationToken);

    // The one atomic operation in this store: creates the child User and the first GuardianLink in
    // one Marten session/SaveChangesAsync, because they must both land or neither does -- see
    // docs/backend/analysis/child-accounts-and-guardian-roles.md ("Provisioning-time atomicity").
    // Takes raw event lists rather than going through IUserEventStore, since the two writes have to
    // share one session -- MartenGuardianLinkEventStore opens it directly against the same
    // IUsersStore that MartenUserEventStore uses.
    Task<(IReadOnlyCollection<UserEvent> UserEvents, IReadOnlyCollection<GuardianEvent> GuardianEvents)> CreateChildAndLinkAsync(
        KeycloakSubject childSubject,
        UserId childId,
        IReadOnlyCollection<UserEvent> userEvents,
        GuardianLinkId linkId,
        IReadOnlyCollection<GuardianEvent> guardianEvents,
        CancellationToken cancellationToken);
}
