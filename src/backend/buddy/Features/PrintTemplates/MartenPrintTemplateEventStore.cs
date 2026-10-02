using buddy.Common.Concurrency;
using buddy.Features.Groups;
using buddy.Features.Users;

using Marten;

namespace buddy.Features.PrintTemplates;

public sealed class MartenPrintTemplateEventStore(IPrintTemplatesStore store) : IPrintTemplateEventStore
{
    public async Task<IReadOnlyCollection<PrintTemplateEvent>> ReadAsync(PrintTemplateId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var events = await session.Events.FetchStreamAsync(id.Value, token: cancellationToken);
        session.ObserveStream(id.Value, events);

        return [.. events.Select(e => PrintTemplateEvent.FromPayload(e.Data))];
    }

    public async Task<PrintTemplate?> FindSnapshotAsync(PrintTemplateId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var snapshot = await session.LoadAsync<PrintTemplateSnapshot>(id.Value, cancellationToken);

        return snapshot?.PrintTemplate;
    }

    public async Task CreateAsync(PrintTemplateId id, IReadOnlyCollection<PrintTemplateEvent> events, PrintTemplateIndexDocument index, CancellationToken cancellationToken)
    {
        if (events.FirstOrDefault() is not (PrintTemplateCreated or PrintTemplateCreatedForGroup))
        {
            throw new InvalidOperationException("The first event of a new print template stream must be a creation event.");
        }

        await using var session = store.LightweightSession();
        session.StartTrackedStream(id.Value, ToPayloads(events));
        session.Store(index);

        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task AppendAsync(PrintTemplateId id, IReadOnlyCollection<PrintTemplateEvent> events, PrintTemplateIndexDocument? index, CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        await using var session = store.LightweightSession();
        session.AppendTracked(id.Value, ToPayloads(events));

        if (index is not null)
        {
            session.Store(index);
        }

        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<PrintTemplateIndexDocument>> ListAsync(UserId userId, IReadOnlyCollection<GroupId> groupIds, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var groupGuids = groupIds.Select(g => g.Value).ToArray();

        // Ordered with a tie-breaker: a Marten query without ORDER BY has no stable row order.
        return await session.Query<PrintTemplateIndexDocument>()
            .Where(d => !d.IsDeleted && (d.OwnerUserId == userId.Value || (d.OwnerGroupId != null && groupGuids.Contains(d.OwnerGroupId.Value))))
            .OrderBy(d => d.Name)
            .ThenBy(d => d.Id)
            .ToListAsync(cancellationToken);
    }

    private static object[] ToPayloads(IReadOnlyCollection<PrintTemplateEvent> events) =>
        [.. events.Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty print template event."))];
}
