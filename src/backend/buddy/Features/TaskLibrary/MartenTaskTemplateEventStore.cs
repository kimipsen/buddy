using buddy.Features.Users;

using Marten;

namespace buddy.Features.TaskLibrary;

public sealed class MartenTaskTemplateEventStore(ITaskLibraryStore store) : ITaskTemplateEventStore
{
    public async Task<IReadOnlyCollection<TaskTemplateEvent>> ReadAsync(TaskTemplateId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var events = await session.Events.FetchStreamAsync(id.Value, token: cancellationToken);

        return [.. events.Select(e => TaskTemplateEvent.FromPayload(e.Data))];
    }

    public async Task<IReadOnlyCollection<TaskTemplateEvent>> CreateAsync(TaskTemplateId id, IReadOnlyCollection<TaskTemplateEvent> events, CancellationToken cancellationToken)
    {
        var childId = events.FirstOrDefault() switch
        {
            TaskTemplateCreated created => created.ChildId,
            _ => throw new InvalidOperationException("The first event of a new task template stream must be TaskTemplateCreated."),
        };

        var payloads = events
            .Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty task template event."))
            .ToArray();

        await using var session = store.LightweightSession();
        session.Events.StartStream(id.Value, payloads);
        session.Store(new TaskTemplateIndexDocument(id.Value, childId.Value));

        await session.SaveChangesAsync(cancellationToken);

        return events;
    }

    public async Task AppendAsync(TaskTemplateId id, IReadOnlyCollection<TaskTemplateEvent> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        var payloads = events
            .Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty task template event."))
            .ToArray();

        await using var session = store.LightweightSession();
        session.Events.Append(id.Value, payloads);

        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<TaskTemplateId>> ListIdsForChildAsync(UserId childId, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();

        var docs = await session.Query<TaskTemplateIndexDocument>()
            .Where(d => d.ChildId == childId.Value)
            .ToListAsync(cancellationToken);

        return [.. docs.Select(d => new TaskTemplateId(d.Id))];
    }

    public async Task<UserId?> FindChildIdForTemplateAsync(TaskTemplateId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();

        var doc = await session.Query<TaskTemplateIndexDocument>()
            .Where(d => d.Id == id.Value)
            .FirstOrDefaultAsync(cancellationToken);

        return doc is null ? null : new UserId(doc.ChildId);
    }
}
