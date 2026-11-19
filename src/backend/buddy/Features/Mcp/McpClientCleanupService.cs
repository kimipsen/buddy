namespace buddy.Features.Mcp;

// Runs McpClientCleaner once a day, the same shape as IdempotencyCleanupService: no startup pass, the first
// pass is after the interval, so a host that starts runs nothing against Keycloak. Registered only with
// Features:Mcp and Mcp:ClientCleanup:Enabled on.
public sealed class McpClientCleanupService(IServiceScopeFactory scopeFactory, TimeProvider time, ILogger<McpClientCleanupService> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, time);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunOnceAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown.
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            await scope.ServiceProvider.GetRequiredService<McpClientCleaner>().RunAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A failed pass deleted nothing and the next one tries again -- never worth taking the host down.
            logger.McpClientCleanupFailed(ex);
        }
    }
}
