using buddy.Features.Users;

using Marten;

namespace buddy.Features.Mealplans;

public sealed class MartenAiCredentialEventStore(IMealplansStore store) : IAiCredentialEventStore
{
    public async Task<IReadOnlyCollection<AiProviderCredentialEvent>> ReadAsync(AiCredentialId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var events = await session.Events.FetchStreamAsync(id.Value, token: cancellationToken);

        return [.. events.Select(e => AiProviderCredentialEvent.FromPayload(e.Data))];
    }

    public async Task<IReadOnlyCollection<AiProviderCredentialEvent>> CreateAsync(AiCredentialId id, IReadOnlyCollection<AiProviderCredentialEvent> events, CancellationToken cancellationToken)
    {
        var childId = events.FirstOrDefault() switch
        {
            AiCredentialsInitialized created => created.ChildId,
            _ => throw new InvalidOperationException("The first event of a new AI credential stream must be AiCredentialsInitialized."),
        };

        var payloads = events
            .Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty AI credential event."))
            .ToArray();

        await using var session = store.LightweightSession();
        session.Events.StartStream(id.Value, payloads);
        session.Store(new AiCredentialIndexDocument(id.Value, childId.Value));

        await session.SaveChangesAsync(cancellationToken);

        return events;
    }

    public async Task AppendAsync(AiCredentialId id, IReadOnlyCollection<AiProviderCredentialEvent> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        var payloads = events
            .Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty AI credential event."))
            .ToArray();

        await using var session = store.LightweightSession();
        session.Events.Append(id.Value, payloads);

        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task<AiCredentialId?> FindIdForChildAsync(UserId childId, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();

        var doc = await session.Query<AiCredentialIndexDocument>()
            .Where(d => d.ChildId == childId.Value)
            .FirstOrDefaultAsync(cancellationToken);

        return doc is null ? null : new AiCredentialId(doc.Id);
    }
}
