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

namespace buddy.Features.VisitSummaries;

// One printable, shareable report per prepared doctor's visit, read live from the sleep diary,
// medicines and feelings (docs/backend/analysis/doctor-visit-summary.md).
public static class VisitSummariesFeature
{
    public const string OpenApiDocumentName = "visitsummaries";

    private static readonly Type[] EventTypes =
    [
        typeof(VisitSummaryCreated),
        typeof(VisitSummaryUpdated),
        typeof(VisitSummaryDeleted),
        typeof(VisitSummaryShareLinkCreated),
        typeof(VisitSummaryShareLinkRevoked)
    ];

    // Depends on IGuardianLinkEventStore for authorization and IUserEventStore for the names on the
    // report, so AddUsersFeature and AddGuardiansFeature must run first. The section sources are
    // registered by the features that own them (AddSleepDiariesFeature, AddMedicinesFeature,
    // AddFeelingsFeature); order doesn't matter for those, they're resolved on first use.
    public static IServiceCollection AddVisitSummariesFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddBuddyOpenApiDocument(OpenApiDocumentName);

        // Shared process-wide pool -- see PostgresDataSource.
        services.AddPostgresDataSource(configuration);
        services.AddSingleton<IPersonalDataEraser, VisitSummariesPersonalDataEraser>();
        services.AddSingleton<IPersonalDataExporter, VisitSummariesPersonalDataExporter>();

        services.AddMartenStore<IVisitSummariesStore>(serviceProvider =>
        {
            var options = new StoreOptions();
            options.Connection(serviceProvider.GetRequiredService<NpgsqlDataSource>());
            options.DatabaseSchemaName = "visitsummaries";
            options.Events.StreamIdentity = StreamIdentity.AsGuid;
            options.Events.AddEventTypes(EventTypes);

            options.UseSystemTextJsonForSerialization(
                enumStorage: EnumStorage.AsString,
                configure: json => json.Converters.Add(new StronglyTypedIdJsonConverterFactory()));

            // Inline snapshots in the shared "snapshots" schema, registered via Register() rather
            // than Projections.Snapshot<T>() -- see PickupsFeature for why.
            options.Projections.Register(new VisitSummarySnapshotProjection(), ProjectionLifecycle.Inline);
            options.Schema.For<VisitSummarySnapshot>().DatabaseSchemaName(SnapshotSchema.Name);
            options.Schema.For<VisitSummaryIndexDocument>().Index(d => d.ChildId);
            options.Schema.For<VisitSummaryShareLinkDocument>().UniqueIndex(d => d.TokenHash).Index(d => d.SummaryId);

            return options;
        });

        services.AddSingleton<IVisitSummaryEventStore, MartenVisitSummaryEventStore>();
        services.AddSingleton<VisitSummaryReports>();

        return services;
    }

    public static IEndpointRouteBuilder MapVisitSummariesFeature(this IEndpointRouteBuilder endpoints)
    {
        var visitSummaries = endpoints.MapGroup("/visit-summaries")
            .WithTags("VisitSummaries")
            .RequireAuthorization()
            .WithGroupName(OpenApiDocumentName)
            .WithETag();

        visitSummaries.MapCreateVisitSummary();
        visitSummaries.MapListVisitSummaries();
        visitSummaries.MapGetVisitSummary();
        visitSummaries.MapUpdateVisitSummary();
        visitSummaries.MapDeleteVisitSummary();
        visitSummaries.MapCreateVisitSummaryShareLink();
        visitSummaries.MapRevokeVisitSummaryShareLink();
        visitSummaries.MapGetSharedVisitSummary();

        return endpoints;
    }
}
