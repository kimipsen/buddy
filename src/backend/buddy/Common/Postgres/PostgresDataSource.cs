using System.Data.Common;

using buddy.Features.Users;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using Npgsql;

namespace buddy.Common.Postgres;

// One NpgsqlDataSource -- i.e. one connection pool -- for the whole process. Every feature
// registers its own Marten store (AddMartenStore<IUsersStore>, <IGroupsStore>, ... nine in total);
// given a connection string, each of them would build a private pool with Npgsql's default
// Maximum Pool Size of 100, so the API could open ~900 connections against a Postgres whose
// max_connections is 100 (and which, in production, Keycloak shares). Instead every store calls
// StoreOptions.Connection(serviceProvider.GetRequiredService<NpgsqlDataSource>()), so a single cap
// bounds all of them together.
//
// The cap defaults to DefaultMaxPoolSize; an explicit Maximum Pool Size in
// ConnectionStrings:Postgres always wins, so each environment can tune it. ApplicationName
// defaults to "buddy" so the API's connections are recognisable in pg_stat_activity.
public static class PostgresDataSource
{
    public const int DefaultMaxPoolSize = 50;

    public const string DefaultApplicationName = "buddy";

    private static readonly string[] MaxPoolSizeKeywords = ["Maximum Pool Size", "MaxPoolSize"];

    private static readonly string[] ApplicationNameKeywords = ["Application Name", "ApplicationName"];

    // Idempotent: every feature calls it, so each one stays self-contained, and the first call wins.
    public static IServiceCollection AddPostgresDataSource(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PostgresOptions>(configuration.GetSection(PostgresOptions.SectionName));

        // Resolved lazily (first store resolution), so configuration overrides added after the
        // features register -- e.g. the integration-test fixture's Testcontainers connection
        // string -- are what the pool is built from.
        services.TryAddSingleton(serviceProvider =>
        {
            var postgres = serviceProvider.GetRequiredService<IOptionsMonitor<PostgresOptions>>().CurrentValue;
            return new NpgsqlDataSourceBuilder(WithDefaults(postgres.Postgres)).Build();
        });

        return services;
    }

    public static string WithDefaults(string connectionString, int maxPoolSize = DefaultMaxPoolSize)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        DbConnectionStringBuilder parsed;
        try
        {
            // Case-insensitive keys; only used to detect explicit settings.
            parsed = new DbConnectionStringBuilder { ConnectionString = connectionString };
        }
        catch (ArgumentException)
        {
            // Not parseable -- leave it alone and let Npgsql report the real error.
            return connectionString;
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        if (!MaxPoolSizeKeywords.Any(parsed.ContainsKey))
        {
            builder.MaxPoolSize = maxPoolSize;
        }

        if (!ApplicationNameKeywords.Any(parsed.ContainsKey))
        {
            builder.ApplicationName = DefaultApplicationName;
        }

        return builder.ConnectionString;
    }
}
