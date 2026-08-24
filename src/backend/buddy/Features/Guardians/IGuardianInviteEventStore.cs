using buddy.Features.Users;

namespace buddy.Features.Guardians;

public interface IGuardianInviteEventStore
{
    Task<IReadOnlyCollection<GuardianInviteEvent>> CreateAsync(GuardianInviteId id, IReadOnlyCollection<GuardianInviteEvent> events, CancellationToken cancellationToken);

    Task AppendAsync(GuardianInviteId id, IReadOnlyCollection<GuardianInviteEvent> events, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<GuardianInviteDocument>> ListPendingInvitesAsync(UserId childId, CancellationToken cancellationToken);

    Task<GuardianInviteDocument?> FindInviteAsync(Guid inviteId, CancellationToken cancellationToken);

    Task<GuardianInviteDocument?> FindPendingInviteAsync(UserId childId, string normalizedEmail, CancellationToken cancellationToken);

    Task<GuardianInviteDocument?> FindInviteByTokenAsync(string token, CancellationToken cancellationToken);

    // Accepting an invite starts a brand-new GuardianLink stream (the child already exists,
    // unlike CreateChildAndLinkAsync's from-scratch provisioning) and marks this invite accepted,
    // in one session/SaveChangesAsync -- the same "both land or neither does" guarantee
    // CreateChildAndLinkAsync gives child provisioning.
    Task AcceptAsync(
        GuardianInviteId inviteId,
        IReadOnlyCollection<GuardianInviteEvent> inviteEvents,
        GuardianLinkId linkId,
        IReadOnlyCollection<GuardianEvent> guardianEvents,
        CancellationToken cancellationToken);
}
