using JasperFx;

using Marten;

namespace buddy.Common.Idempotency;

public sealed class IdempotencyKeyRepository(IIdempotencyStore store)
{
    public async Task<IdempotencyRecord?> FindAsync(Guid userId, string key, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();

        return await session.LoadAsync<IdempotencyRecord>(IdempotencyRecord.BuildId(userId, key), cancellationToken);
    }

    // Claims the key for this request by inserting an InProgress row -- Insert (not Store) is
    // what actually guards against a concurrent duplicate across processes, the same reason
    // MartenUserEventStore.CreateAsync uses it for KeycloakIdentity. False means another request
    // (this one's own retry-in-flight, or a genuine concurrent duplicate) already holds the key.
    public async Task<bool> TryReserveAsync(Guid userId, string key, string fingerprint, CancellationToken cancellationToken)
    {
        var record = new IdempotencyRecord(
            IdempotencyRecord.BuildId(userId, key), userId, key, fingerprint,
            IdempotencyStatus.InProgress, ResponseStatusCode: null, ResponseContentType: null, ResponseBody: null, DateTimeOffset.UtcNow);

        await using var session = store.LightweightSession();
        session.Insert(record);

        try
        {
            await session.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DocumentAlreadyExistsException)
        {
            return false;
        }
    }

    public async Task CompleteAsync(Guid userId, string key, int statusCode, string? contentType, byte[] responseBody, CancellationToken cancellationToken)
    {
        var id = IdempotencyRecord.BuildId(userId, key);

        await using var session = store.LightweightSession();
        var existing = await session.LoadAsync<IdempotencyRecord>(id, cancellationToken);

        if (existing is null)
        {
            // Reservation was already cleaned up (e.g. it aged past the InProgress cutoff while
            // this request was still running) -- nothing left to complete.
            return;
        }

        session.Store(existing with
        {
            Status = IdempotencyStatus.Completed,
            ResponseStatusCode = statusCode,
            ResponseContentType = contentType,
            ResponseBody = responseBody,
        });

        await session.SaveChangesAsync(cancellationToken);
    }

    // Called when the wrapped request throws instead of completing -- drops the reservation so a
    // genuine retry isn't blocked behind a request that never got a response of its own.
    public async Task ReleaseAsync(Guid userId, string key, CancellationToken cancellationToken)
    {
        await using var session = store.LightweightSession();
        session.Delete<IdempotencyRecord>(IdempotencyRecord.BuildId(userId, key));

        await session.SaveChangesAsync(cancellationToken);
    }

    // Completed rows are kept for `completedRetention` so a delayed retry can still replay them;
    // InProgress rows older than `inProgressTimeout` are treated as abandoned (the process that
    // reserved them crashed or was killed before completing) and cleared so the key becomes
    // claimable again. Runs in bounded batches so one pass never holds an unbounded transaction.
    public async Task<int> DeleteExpiredAsync(TimeSpan completedRetention, TimeSpan inProgressTimeout, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var completedCutoff = now - completedRetention;
        var inProgressCutoff = now - inProgressTimeout;
        var totalDeleted = 0;

        while (true)
        {
            await using var session = store.LightweightSession();

            var staleIds = await session.Query<IdempotencyRecord>()
                .Where(r =>
                    (r.Status == IdempotencyStatus.Completed && r.CreatedAt < completedCutoff) ||
                    (r.Status == IdempotencyStatus.InProgress && r.CreatedAt < inProgressCutoff))
                .Select(r => r.Id)
                .Take(1000)
                .ToListAsync(cancellationToken);

            if (staleIds.Count == 0)
            {
                return totalDeleted;
            }

            foreach (var id in staleIds)
            {
                session.Delete<IdempotencyRecord>(id);
            }

            await session.SaveChangesAsync(cancellationToken);
            totalDeleted += staleIds.Count;
        }
    }
}
