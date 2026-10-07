namespace buddy.Features.Privacy;

// The safety net under UserErasure (see docs/backend/analysis/gdpr-data-protection.md): finishes
// erasures that stopped halfway (Postgres or Keycloak down mid-request), erases users deleted before
// erasure existed, and re-erases users a database restore brought back. Runs shortly after startup
// -- which is when a restore shows -- and then every 15 minutes.
public sealed class UserErasureService(IServiceScopeFactory scopeFactory, ILogger<UserErasureService> logger) : BackgroundService
{
    private static readonly TimeSpan FirstSweepDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(FirstSweepDelay, stoppingToken);

            using var timer = new PeriodicTimer(Interval);

            do
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<UserErasure>().FinishUnfinishedAsync(stoppingToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // Nothing is lost: the same users are found again on the next sweep.
                    logger.ErasureSweepFailed(exception);
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutting down.
        }
    }
}
