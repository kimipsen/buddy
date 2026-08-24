using buddy.Features.Users;

using Marten;

namespace buddy.Features.Guardians;

// Takes IUsersStore, not a dedicated store of its own -- a guardian invite's stream lives in the
// same Marten store/schema ("users") as User and GuardianLink, the same reason
// MartenGuardianLinkEventStore does.
public sealed class MartenGuardianInviteEventStore(IUsersStore store) : IGuardianInviteEventStore
{
    public async Task<IReadOnlyCollection<GuardianInviteEvent>> CreateAsync(GuardianInviteId id, IReadOnlyCollection<GuardianInviteEvent> events, CancellationToken cancellationToken)
    {
        if (events.FirstOrDefault() is not GuardianInviteCreated created)
        {
            throw new InvalidOperationException("The first event of a new guardian invite stream must be GuardianInviteCreated.");
        }

        var payloads = events
            .Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty guardian invite event."))
            .ToArray();

        await using var session = store.LightweightSession();
        session.Events.StartStream(id.Value, payloads);
        session.Store(new GuardianInviteDocument(
            id.Value,
            created.ChildId.Value,
            created.ChildGivenName,
            created.InvitedEmail,
            created.Kind,
            created.InvitedBy.Value,
            created.TokenHash,
            created.OccurredAt,
            created.ExpiresAt,
            GuardianInviteStatus.Pending));

        await session.SaveChangesAsync(cancellationToken);

        return events;
    }

    public async Task AppendAsync(GuardianInviteId id, IReadOnlyCollection<GuardianInviteEvent> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        var payloads = events
            .Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty guardian invite event."))
            .ToArray();

        await using var session = store.LightweightSession();
        session.Events.Append(id.Value, payloads);

        foreach (var @event in events)
        {
            switch (@event)
            {
                // A resend: same InviteId, refreshed token/expiry -- upserts the same document
                // row with the new values, mirroring MartenGroupEventStore's GroupInviteCreated
                // case (this is never the stream's first event -- CreateAsync handles that).
                case GuardianInviteCreated created:
                    session.Store(new GuardianInviteDocument(
                        id.Value,
                        created.ChildId.Value,
                        created.ChildGivenName,
                        created.InvitedEmail,
                        created.Kind,
                        created.InvitedBy.Value,
                        created.TokenHash,
                        created.OccurredAt,
                        created.ExpiresAt,
                        GuardianInviteStatus.Pending));
                    break;

                case GuardianInviteAccepted:
                    var accepted = await session.LoadAsync<GuardianInviteDocument>(id.Value, cancellationToken);
                    if (accepted is not null)
                    {
                        session.Store(accepted with { Status = GuardianInviteStatus.Accepted });
                    }
                    break;

                case GuardianInviteRevoked:
                    var revoked = await session.LoadAsync<GuardianInviteDocument>(id.Value, cancellationToken);
                    if (revoked is not null)
                    {
                        session.Store(revoked with { Status = GuardianInviteStatus.Revoked });
                    }
                    break;
            }
        }

        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<GuardianInviteDocument>> ListPendingInvitesAsync(UserId childId, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();

        return await session.Query<GuardianInviteDocument>()
            .Where(d => d.ChildId == childId.Value && d.Status == GuardianInviteStatus.Pending)
            .ToListAsync(cancellationToken);
    }

    public async Task<GuardianInviteDocument?> FindInviteAsync(Guid inviteId, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();

        return await session.LoadAsync<GuardianInviteDocument>(inviteId, cancellationToken);
    }

    public async Task<GuardianInviteDocument?> FindPendingInviteAsync(UserId childId, string normalizedEmail, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();

        return await session.Query<GuardianInviteDocument>()
            .Where(d => d.ChildId == childId.Value && d.InvitedEmail == normalizedEmail && d.Status == GuardianInviteStatus.Pending)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<GuardianInviteDocument?> FindInviteByTokenAsync(string token, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var tokenHash = GuardianInviteToken.Hash(token);

        return await session.Query<GuardianInviteDocument>()
            .Where(d => d.TokenHash == tokenHash)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AcceptAsync(
        GuardianInviteId inviteId,
        IReadOnlyCollection<GuardianInviteEvent> inviteEvents,
        GuardianLinkId linkId,
        IReadOnlyCollection<GuardianEvent> guardianEvents,
        CancellationToken cancellationToken)
    {
        if (guardianEvents.FirstOrDefault() is not GuardianLinked linked)
        {
            throw new InvalidOperationException("The first event of a new guardian link stream must be GuardianLinked.");
        }

        var invitePayloads = inviteEvents
            .Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty guardian invite event."))
            .ToArray();

        var guardianPayloads = guardianEvents
            .Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty guardian event."))
            .ToArray();

        await using var session = store.LightweightSession();

        session.Events.Append(inviteId.Value, invitePayloads);
        session.Events.StartStream(linkId.Value, guardianPayloads);

        var invite = await session.LoadAsync<GuardianInviteDocument>(inviteId.Value, cancellationToken)
            ?? throw new InvalidOperationException($"No GuardianInviteDocument found for invite '{inviteId.Value}'.");
        session.Store(invite with { Status = GuardianInviteStatus.Accepted });

        session.Store(new GuardianLinkDocument(
            GuardianLinkDocument.BuildId(linked.ChildId.Value, linked.GuardianId.Value),
            linkId.Value,
            linked.ChildId.Value,
            linked.GuardianId.Value,
            linked.Kind,
            IsRevoked: false));

        await session.SaveChangesAsync(cancellationToken);
    }
}
