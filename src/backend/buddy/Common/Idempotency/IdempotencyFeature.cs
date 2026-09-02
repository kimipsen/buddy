using buddy.Features.Users;
using buddy.Serialization;

using Marten;

using Microsoft.Extensions.Options;

using Weasel.Core;

namespace buddy.Common.Idempotency;

public static class IdempotencyFeature
{
    public static IServiceCollection AddIdempotencyFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PostgresOptions>(configuration.GetSection(PostgresOptions.SectionName));

        services.AddMartenStore<IIdempotencyStore>(serviceProvider =>
        {
            var postgres = serviceProvider.GetRequiredService<IOptionsMonitor<PostgresOptions>>().CurrentValue;

            var options = new StoreOptions();
            options.Connection(postgres.Postgres);
            options.DatabaseSchemaName = "idempotency";

            options.UseSystemTextJsonForSerialization(
                enumStorage: EnumStorage.AsString,
                configure: json => json.Converters.Add(new StronglyTypedIdJsonConverterFactory()));

            return options;
        });

        services.AddSingleton<IdempotencyKeyRepository>();
        services.AddHostedService<IdempotencyCleanupService>();

        return services;
    }

    // Placed after authentication/authorization but before the feature Map*() calls in
    // Program.cs -- endpoint execution runs as the pipeline's implicit terminal step regardless
    // of where Map*() is called, so this still wraps it.
    public static IApplicationBuilder UseIdempotencyKeys(this IApplicationBuilder app) =>
        app.UseMiddleware<IdempotencyKeyMiddleware>();
}
