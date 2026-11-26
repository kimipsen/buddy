using JasperFx;
using JasperFx.Events;

using Marten;

namespace buddy.Common.Concurrency;

public static class StreamStart
{
    // Saves a session that started a stream. A second start on an existing id throws
    // ExistingStreamIdCollisionException (Marten's, thrown by SaveChangesAsync; JasperFx.Events has a
    // same-named one, covered too), which derive from System.Exception and not from
    // ConcurrencyException, so ConcurrencyConflictMiddleware would let it surface as a 500. Two
    // requests creating the same lazily created stream are a lost race like any other, so it becomes
    // a ConcurrencyException (409 concurrency_conflict).
    public static async Task SaveStartedStreamAsync(this IDocumentSession session, string stream, CancellationToken cancellationToken)
    {
        try
        {
            await session.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is Marten.Exceptions.ExistingStreamIdCollisionException or ExistingStreamIdCollisionException)
        {
            throw new ConcurrencyException($"The {stream} stream was created by another request.", exception);
        }
    }
}
