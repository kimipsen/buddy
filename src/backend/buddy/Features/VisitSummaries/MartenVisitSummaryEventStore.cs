using buddy.Common.Concurrency;
using buddy.Features.Users;

using Marten;

namespace buddy.Features.VisitSummaries;

public sealed class MartenVisitSummaryEventStore(IVisitSummariesStore store) : IVisitSummaryEventStore
{
    public async Task<IReadOnlyCollection<VisitSummaryEvent>> ReadAsync(VisitSummaryId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var events = await session.Events.FetchStreamAsync(id.Value, token: cancellationToken);
        session.ObserveStream(id.Value, events);

        return [.. events.Select(e => VisitSummaryEvent.FromPayload(e.Data))];
    }

    public async Task<VisitSummary?> FindSnapshotAsync(VisitSummaryId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var snapshot = await session.LoadAsync<VisitSummarySnapshot>(id.Value, cancellationToken);

        return snapshot?.VisitSummary;
    }

    public async Task CreateAsync(VisitSummaryId id, IReadOnlyCollection<VisitSummaryEvent> events, CancellationToken cancellationToken)
    {
        var created = events.FirstOrDefault() switch
        {
            VisitSummaryCreated e => e,
            _ => throw new InvalidOperationException("The first event of a new visit summary stream must be VisitSummaryCreated."),
        };

        await using var session = store.LightweightSession();
        session.StartTrackedStream(id.Value, ToPayloads(events));
        session.Store(new VisitSummaryIndexDocument(id.Value, created.ChildId.Value, IsDeleted: false));

        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task AppendAsync(VisitSummaryId id, IReadOnlyCollection<VisitSummaryEvent> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        await using var session = store.LightweightSession();
        session.AppendTracked(id.Value, ToPayloads(events));

        foreach (var @event in events)
        {
            await UpdateDocumentsAsync(session, id, @event, cancellationToken);
        }

        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<VisitSummaryIndexDocument>> ListForChildAsync(UserId childId, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();

        return await session.Query<VisitSummaryIndexDocument>()
            .Where(d => d.ChildId == childId.Value)
            .ToListAsync(cancellationToken);
    }

    public async Task<VisitSummaryShareLinkDocument?> FindLinkByHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();

        return await session.Query<VisitSummaryShareLinkDocument>()
            .Where(d => d.TokenHash == tokenHash)
            .FirstOrDefaultAsync(cancellationToken);
    }

    // The lookup documents follow the stream in the same transaction as the append.
    private static async Task UpdateDocumentsAsync(IDocumentSession session, VisitSummaryId id, VisitSummaryEvent @event, CancellationToken cancellationToken)
    {
        switch (@event)
        {
            case VisitSummaryShareLinkCreated created:
                var index = await session.LoadAsync<VisitSummaryIndexDocument>(id.Value, cancellationToken);
                session.Store(new VisitSummaryShareLinkDocument(
                    created.LinkId.Value,
                    id.Value,
                    index?.ChildId ?? throw new InvalidOperationException($"Visit summary {id.Value} has no index document."),
                    created.TokenHash,
                    created.ExpiresAt,
                    IsRevoked: false));
                break;

            case VisitSummaryShareLinkRevoked revoked:
                if (await session.LoadAsync<VisitSummaryShareLinkDocument>(revoked.LinkId.Value, cancellationToken) is { } link)
                {
                    session.Store(link with { IsRevoked = true });
                }

                break;

            case VisitSummaryDeleted:
                if (await session.LoadAsync<VisitSummaryIndexDocument>(id.Value, cancellationToken) is { } summary)
                {
                    session.Store(summary with { IsDeleted = true });
                }

                var links = await session.Query<VisitSummaryShareLinkDocument>()
                    .Where(d => d.SummaryId == id.Value)
                    .ToListAsync(cancellationToken);

                foreach (var live in links.Where(l => !l.IsRevoked))
                {
                    session.Store(live with { IsRevoked = true });
                }

                break;

            case VisitSummaryCreated or VisitSummaryUpdated:
                break;
        }
    }

    private static object[] ToPayloads(IReadOnlyCollection<VisitSummaryEvent> events) =>
        [.. events.Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty visit summary event."))];
}
