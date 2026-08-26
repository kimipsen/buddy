using buddy.Features.Users;
using buddy.Serialization;

using JasperFx.Events;

using Marten;

using Microsoft.Extensions.Options;

using Weasel.Core;

namespace buddy.Features.Progress;

// RecordStarChange (see that folder) has no endpoint of its own -- it's only ever called
// explicitly from other features' handlers, never over HTTP. GetMyProgress is the one read
// endpoint this feature needs so the child dashboard has something to show.
public static class ProgressFeature
{
    public const string OpenApiDocumentName = "progress";

    private static readonly Type[] EventTypes =
    [
        typeof(ProgressStarted),
        typeof(StarAwarded),
        typeof(StarRevoked),
        typeof(MilestoneUnlocked)
    ];

    public static IServiceCollection AddProgressFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOpenApi(OpenApiDocumentName, options =>
        {
            options.ShouldInclude = api => api.GroupName == OpenApiDocumentName;
        });

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

    public static IEndpointRouteBuilder MapProgressFeature(this IEndpointRouteBuilder endpoints)
    {
        var progress = endpoints.MapGroup("/progress")
            .WithTags("Progress")
            .RequireAuthorization()
            .WithGroupName(OpenApiDocumentName);

        progress.MapGetMyProgress();

        return endpoints;
    }
}
