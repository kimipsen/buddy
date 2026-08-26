using buddy.Features.Users;
using buddy.Serialization;

using JasperFx.Events;

using Marten;

using Microsoft.Extensions.Options;

using Weasel.Core;

namespace buddy.Features.Progress;

// No MapProgressFeature yet -- this feature has no HTTP endpoints of its own. It's driven
// entirely by explicit calls from other features' handlers (see RecordStarChange). A read
// endpoint for the child dashboard is Phase 2 frontend work, not sketched here.
public static class ProgressFeature
{
    private static readonly Type[] EventTypes =
    [
        typeof(ProgressStarted),
        typeof(StarAwarded),
        typeof(StarRevoked),
        typeof(MilestoneUnlocked)
    ];

    public static IServiceCollection AddProgressFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PostgresOptions>(configuration.GetSection(PostgresOptions.SectionName));

        services.AddMartenStore<IProgressStore>(serviceProvider =>
        {
            var postgres = serviceProvider.GetRequiredService<IOptionsMonitor<PostgresOptions>>().CurrentValue;

            var options = new StoreOptions();
            options.Connection(postgres.Postgres);
            options.DatabaseSchemaName = "progress";
            options.Events.StreamIdentity = StreamIdentity.AsGuid;
            options.Events.AddEventTypes(EventTypes);

            options.UseSystemTextJsonForSerialization(
                enumStorage: EnumStorage.AsString,
                configure: json => json.Converters.Add(new StronglyTypedIdJsonConverterFactory()));

            return options;
        });

        services.AddSingleton<IProgressEventStore, MartenProgressEventStore>();

        return services;
    }
}
