using buddy.Common.Postgres;
using buddy.Serialization;

using JasperFx.Events;
using JasperFx.Events.Projections;

using Marten;

using Npgsql;

using Weasel.Core;

namespace buddy.Features.PrintTemplates;

public static class PrintTemplatesFeature
{
    public const string OpenApiDocumentName = "printtemplates";

    private static readonly Type[] EventTypes =
    [
        typeof(PrintTemplateCreated),
        typeof(PrintTemplateCreatedForGroup),
        typeof(PrintTemplateRenamed),
        typeof(PrintTemplateLayoutChanged),
        typeof(PrintTemplateRowsReplaced),
        typeof(PrintTemplateGuardianColorsReplaced),
        typeof(PrintTemplateDeleted)
    ];

    // Write-time reference checks read other features' stores (Guardians, Groups, Calendars,
    // WorkLocations), so all of those must be registered first.
    public static IServiceCollection AddPrintTemplatesFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOpenApi(OpenApiDocumentName, options =>
        {
            options.ShouldInclude = api => api.GroupName == OpenApiDocumentName;
        });

        // Shared process-wide pool -- see PostgresDataSource.
        services.AddPostgresDataSource(configuration);

        services.AddMartenStore<IPrintTemplatesStore>(serviceProvider =>
        {
            var options = new StoreOptions();
            options.Connection(serviceProvider.GetRequiredService<NpgsqlDataSource>());
            options.DatabaseSchemaName = "printtemplates";
            options.Events.StreamIdentity = StreamIdentity.AsGuid;
            options.Events.AddEventTypes(EventTypes);

            options.UseSystemTextJsonForSerialization(
                enumStorage: EnumStorage.AsString,
                configure: json =>
                {
                    json.Converters.Add(new StronglyTypedIdJsonConverterFactory());
                    // PrintTemplate.Owner is an aggregate-only union whose two cases serialize
                    // identically -- only the snapshot ever stores it. See
                    // PrintTemplateOwnerJsonConverter.
                    json.Converters.Add(new PrintTemplateOwnerJsonConverter());
                });

            // Inline snapshot in the shared "snapshots" schema, registered via Register() for the
            // same reason as PickupsFeature. See docs/backend/analysis/event-stream-snapshots.md.
            options.Projections.Register(new PrintTemplateSnapshotProjection(), ProjectionLifecycle.Inline);
            options.Schema.For<PrintTemplateSnapshot>().DatabaseSchemaName(SnapshotSchema.Name);

            return options;
        });

        services.AddSingleton<IPrintTemplateEventStore, MartenPrintTemplateEventStore>();

        return services;
    }

    public static IEndpointRouteBuilder MapPrintTemplatesFeature(this IEndpointRouteBuilder endpoints)
    {
        var printTemplates = endpoints.MapGroup("/print-templates")
            .WithTags("PrintTemplates")
            .RequireAuthorization()
            .WithGroupName(OpenApiDocumentName);

        printTemplates.MapCreatePrintTemplate();
        printTemplates.MapListPrintTemplates();
        printTemplates.MapGetPrintTemplate();
        printTemplates.MapRenamePrintTemplate();
        printTemplates.MapUpdatePrintTemplateLayout();
        printTemplates.MapReplacePrintTemplateRows();
        printTemplates.MapReplacePrintTemplateGuardianColors();
        printTemplates.MapDeletePrintTemplate();

        return endpoints;
    }
}
