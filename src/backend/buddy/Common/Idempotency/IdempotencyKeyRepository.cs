using System.Security.Cryptography;

using JasperFx;

using Marten;

using Microsoft.AspNetCore.DataProtection;

namespace buddy.Common.Idempotency;

// Stored response bodies are encrypted with Data Protection: a body can hold personal data, such as
// the temporary password CreateChild returns, and sits in the database for up to 24 hours.
public sealed class IdempotencyKeyRepository(IIdempotencyStore store, IDataProtectionProvider dataProtection)
{
    private readonly IDataProtector _protector = dataProtection.CreateProtector("buddy.idempotency.response.v1");

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
            Response: null, DateTimeOffset.UtcNow);

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

        session.Store(existing with { Response = new CompletedResponse(statusCode, contentType, _protector.Protect(responseBody)) });

        await session.SaveChangesAsync(cancellationToken);
    }

    // False when the body can't be decrypted -- the key ring changed since it was stored, or it was
    // stored unencrypted before encryption existed. The caller must not run the request again.
    public bool TryReadBody(CompletedResponse response, out byte[] body)
    {
        if (response.Body.Length == 0)
        {
            body = [];
            return true;
        }

        try
        {
            body = _protector.Unprotect(response.Body);
            return true;
        }
        catch (CryptographicException)
        {
            body = [];
            return false;
        }
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
    // rows with no stored response yet (in progress) older than `inProgressTimeout` are treated as abandoned (the process that
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
                    (r.Response != null && r.CreatedAt < completedCutoff) ||
                    (r.Response == null && r.CreatedAt < inProgressCutoff))
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
