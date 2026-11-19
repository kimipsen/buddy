namespace buddy.Features.Mcp;

// Says once, at startup, that the client cleanup is off. AddMcpFeature runs while the ServiceCollection is
// built, when no ILogger exists, so the line is written from a hosted service instead. Registered only when
// Features:Mcp is on and Mcp:ClientCleanup:Enabled is off.
internal sealed class McpStartupLogger(ILogger<McpStartupLogger> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.McpClientCleanupDisabled();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
