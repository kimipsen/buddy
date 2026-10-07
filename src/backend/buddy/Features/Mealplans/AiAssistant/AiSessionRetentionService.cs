namespace buddy.Features.Mealplans;

// Runs AiSessionRetention shortly after startup and then once a day.
public sealed class AiSessionRetentionService(IServiceScopeFactory scopeFactory, ILogger<AiSessionRetentionService> logger) : BackgroundService
{
    private static readonly TimeSpan FirstSweepDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Interval = TimeSpan.FromDays(1);

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
                    await scope.ServiceProvider.GetRequiredService<AiSessionRetention>().EraseExpiredAsync(DateTimeOffset.UtcNow, stoppingToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // Nothing is lost: the same sessions are candidates again on the next sweep.
                    logger.AiSessionRetentionSweepFailed(exception);
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
