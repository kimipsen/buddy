using Microsoft.Extensions.Diagnostics.HealthChecks;

using Npgsql;

namespace buddy.Common.Health;

// Goes through the shared NpgsqlDataSource (see PostgresDataSource), so it tests the same pool and
// connection string every Marten store uses, and never opens a connection outside its cap.
public sealed class PostgresHealthCheck(NpgsqlDataSource dataSource) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var command = dataSource.CreateCommand("SELECT 1");
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is NpgsqlException or TimeoutException or InvalidOperationException)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, exception: exception);
        }
    }
}
