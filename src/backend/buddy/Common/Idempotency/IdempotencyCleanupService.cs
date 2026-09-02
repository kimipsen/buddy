using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace buddy.Common.Idempotency;

// Idempotency records are the only thing in this codebase that need a TTL rather than an
// explicit revoke/delete, so this is the only BackgroundService here -- everything else is
// driven by an HTTP request or an explicit command.
public sealed class IdempotencyCleanupService(IServiceScopeFactory scopeFactory, ILogger<IdempotencyCleanupService> logger) : BackgroundService
{
    public static readonly TimeSpan CompletedRetention = TimeSpan.FromHours(24);
    public static readonly TimeSpan InProgressTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IdempotencyKeyRepository>();
                var deleted = await repository.DeleteExpiredAsync(CompletedRetention, InProgressTimeout, stoppingToken);

                if (deleted > 0)
                {
                    logger.LogInformation("Deleted {Count} expired idempotency record(s).", deleted);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A failed cleanup pass just leaves the stale rows for the next tick to catch --
                // never worth taking the whole host down over.
                logger.LogError(ex, "Idempotency cleanup pass failed.");
            }
        }
    }
}
