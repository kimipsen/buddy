using buddy.Common.Erasure;
using buddy.Common.Http;
using buddy.Common.OpenApi;
using buddy.Common.Postgres;
using buddy.Serialization;

using JasperFx.Events;
using JasperFx.Events.Projections;

using Marten;

using Npgsql;

using Weasel.Core;

namespace buddy.Features.HouseRules;

public static class HouseRulesFeature
{
    public const string OpenApiDocumentName = "houserules";

    private static readonly Type[] EventTypes =
    [
        typeof(RuleBookStarted),
        typeof(RuleAdded),
        typeof(RuleEdited),
        typeof(RuleRemoved),
        typeof(RulesReordered),
        typeof(RuleAcknowledged)
    ];

    // Depends on IGuardianLinkEventStore (personal books), IGroupEventStore (household books) and
    // IUserEventStore (the child's name on GetChildRules), so AddUsersFeature, AddGuardiansFeature and
    // AddGroupsFeature must run first.
    public static IServiceCollection AddHouseRulesFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddBuddyOpenApiDocument(OpenApiDocumentName);

        // Shared process-wide pool -- see PostgresDataSource.
        services.AddPostgresDataSource(configuration);
        // Erases this feature's part of a person (docs/backend/analysis/gdpr-data-protection.md).
        services.AddSingleton<IPersonalDataEraser, HouseRulesPersonalDataEraser>();
        services.AddSingleton<IPersonalDataExporter, HouseRulesPersonalDataExporter>();

        services.AddMartenStore<IHouseRulesStore>(serviceProvider =>
        {
            var options = new StoreOptions();
            options.Connection(serviceProvider.GetRequiredService<NpgsqlDataSource>());
            options.DatabaseSchemaName = "houserules";
            options.Events.StreamIdentity = StreamIdentity.AsGuid;
            options.Events.AddEventTypes(EventTypes);

            options.UseSystemTextJsonForSerialization(
                enumStorage: EnumStorage.AsString,
                configure: json =>
                {
                    json.Converters.Add(new StronglyTypedIdJsonConverterFactory());
                    // Needed for RuleBook.Acknowledgements: ImmutableDictionary<(RuleId, UserId), int>.
                    json.Converters.Add(new ValueTupleJsonConverterFactory());
                });

            // Inline snapshot in the shared "snapshots" schema, registered via Register() rather
            // than Projections.Snapshot<T>() -- see PickupsFeature for why.
            options.Projections.Register(new RuleBookSnapshotProjection(), ProjectionLifecycle.Inline);
            options.Schema.For<RuleBookSnapshot>().DatabaseSchemaName(SnapshotSchema.Name);

            return options;
        });

        services.AddSingleton<IRuleBookEventStore, MartenRuleBookEventStore>();

        return services;
    }

    public static IEndpointRouteBuilder MapHouseRulesFeature(this IEndpointRouteBuilder endpoints)
    {
        var houseRules = endpoints.MapGroup("/house-rules")
            .WithTags("HouseRules")
            .RequireAuthorization()
            .WithGroupName(OpenApiDocumentName)
            .WithETag();

        houseRules.MapAddRule();
        houseRules.MapEditRule();
        houseRules.MapRemoveRule();
        houseRules.MapReorderRules();
        houseRules.MapListRules();
        houseRules.MapAcknowledgeRule();
        houseRules.MapGetChildRules();

        return endpoints;
    }
}
