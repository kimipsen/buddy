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
    //
    // A snapshot whose stream is gone (events wiped or partially restored while the snapshots schema
    // stayed) can't be rebuilt -- Marten would store a null document -- so it is deleted instead: it
    // is the only copy of the personal data left. Returns false in that case, so the caller doesn't
    // append to a stream that no longer exists.
    public static async Task<bool> MaskStreamAsync<TSnapshot>(this IDocumentStore store, Guid streamId, CancellationToken cancellationToken, Func<IEvent, bool>? filter = null)
        where TSnapshot : class
    {
        await EnsureEventStorageAsync(store, cancellationToken);

        await using (var query = store.QuerySession())
        {
            if (await query.Events.FetchStreamStateAsync(streamId, cancellationToken) is null)
            {
                await using var session = store.LightweightSession();
                session.Delete<TSnapshot>(streamId);
                await session.SaveChangesAsync(cancellationToken);

                return false;
            }
        }

        await store.MaskStreamAsync(streamId, cancellationToken, filter);
        await store.Advanced.RebuildSingleStreamAsync<TSnapshot>(streamId, cancellationToken);

        return true;
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
