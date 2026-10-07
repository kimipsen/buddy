using JasperFx.Events;

using Marten;

namespace buddy.Common.Erasure;

// The two erasure operations on a Marten store (see docs/backend/analysis/gdpr-data-protection.md).
public static class StreamErasure
{
    // For a stream that belongs only to the erased person: the events, its snapshot and its index
    // documents go. Deleting a missing stream or document is a no-op, so this is idempotent.
    // deleteDocuments removes the stream's index documents in the same session as the snapshot; a
    // callback because Marten's Delete is generic over the document type.
    public static async Task DeleteStreamAsync<TSnapshot>(
        this IDocumentStore store,
        Guid streamId,
        CancellationToken cancellationToken,
        Action<IDocumentSession>? deleteDocuments = null)
        where TSnapshot : class
    {
        await EnsureEventStorageAsync(store, cancellationToken);
        await store.Advanced.Clean.DeleteSingleEventStreamAsync(streamId, ct: cancellationToken);

        await using var session = store.LightweightSession();
        session.Delete<TSnapshot>(streamId);
        deleteDocuments?.Invoke(session);

        await session.SaveChangesAsync(cancellationToken);
    }

    // For a stream that stays: the store's masking rules (AddMaskingRuleForProtectedInformation) are
    // applied to its events -- all of them, or those the filter picks -- and the snapshot is rebuilt
    // from the masked events, because masking doesn't run projections. Masking a masked event changes
    // nothing, so this is idempotent too.
    public static async Task MaskStreamAsync<TSnapshot>(this IDocumentStore store, Guid streamId, CancellationToken cancellationToken, Func<IEvent, bool>? filter = null)
        where TSnapshot : class
    {
        await store.MaskStreamAsync(streamId, cancellationToken, filter);
        await store.Advanced.RebuildSingleStreamAsync<TSnapshot>(streamId, cancellationToken);
    }

    // For a stream with no snapshot projection (its read model is a document the caller rewrites).
    public static async Task MaskStreamAsync(this IDocumentStore store, Guid streamId, CancellationToken cancellationToken, Func<IEvent, bool>? filter = null)
    {
        await EnsureEventStorageAsync(store, cancellationToken);
        await store.Advanced.ApplyEventDataMasking(
            masking => _ = filter is null ? masking.IncludeStream(streamId) : masking.IncludeStream(streamId, filter),
            cancellationToken);
    }

    // Marten creates a store's tables on first use, so a feature the person never used may have no
    // events table yet -- and the erasure operations, unlike an append, don't create it.
    private static ValueTask EnsureEventStorageAsync(IDocumentStore store, CancellationToken cancellationToken) =>
        store.Storage.Database.EnsureStorageExistsAsync(typeof(IEvent), cancellationToken);
}
