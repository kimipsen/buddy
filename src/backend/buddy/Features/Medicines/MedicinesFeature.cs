using buddy.Common.Erasure;
using buddy.Common.Http;
using buddy.Common.Postgres;
using buddy.Serialization;

using JasperFx.Events;
using JasperFx.Events.Projections;

using Marten;

using Npgsql;

using Weasel.Core;

namespace buddy.Features.Medicines;

public static class MedicinesFeature
{
    public const string OpenApiDocumentName = "medicines";

    private static readonly Type[] EventTypes =
    [
        typeof(MedicineScheduleCreated),
        typeof(MedicineDetailsUpdated),
        typeof(MedicineScheduleRescheduled),
        typeof(MedicineScheduleStopped),
        typeof(DoseStatusChanged),
        typeof(MedicineSharedWithGroup),
        typeof(MedicineUnsharedFromGroup)
    ];

    // Depends on IGuardianLinkEventStore for authorization, so AddGuardiansFeature must run first
    // -- same DI ordering constraint Calendars already has relative to Guardians.
    public static IServiceCollection AddMedicinesFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOpenApi(OpenApiDocumentName, options =>
        {
            options.ShouldInclude = api => api.GroupName == OpenApiDocumentName;
        });

        // Shared process-wide pool -- see PostgresDataSource.
        services.AddPostgresDataSource(configuration);
        // Erases this feature's part of a person (docs/backend/analysis/gdpr-data-protection.md).
        services.AddSingleton<IPersonalDataEraser, MedicinesPersonalDataEraser>();

        services.AddMartenStore<IMedicinesStore>(serviceProvider =>
        {
            var options = new StoreOptions();
            options.Connection(serviceProvider.GetRequiredService<NpgsqlDataSource>());
            options.DatabaseSchemaName = "medicines";
            options.Events.StreamIdentity = StreamIdentity.AsGuid;
            options.Events.AddEventTypes(EventTypes);

            options.UseSystemTextJsonForSerialization(
                enumStorage: EnumStorage.AsString,
                configure: json =>
                {
                    json.Converters.Add(new StronglyTypedIdJsonConverterFactory());
                    // Needed for MedicineSchedule.DoseLog: ImmutableDictionary<(DateOnly, TimeOnly), DoseStatus>
                    // -- plain System.Text.Json throws NotSupportedException for a ValueTuple-keyed
                    // dictionary. MedicineSharing has no tuple fields, but both snapshot documents
                    // share this same StoreOptions/serializer, so one registration covers both.
                    json.Converters.Add(new ValueTupleJsonConverterFactory());
                });

            // Inline snapshots of MedicineSchedule and MedicineSharing, kept transactionally
            // consistent with every event append. Routed to a schema separate from "medicines" --
            // they're derived/rebuildable read state, never the source of truth. See
            // docs/backend/analysis/event-stream-snapshots.md.
            //
            // Registered explicitly via Register(), not the Projections.Snapshot<T>() convenience
            // method: that method tries to auto-derive the document's TId via reflection, which
            // throws (ArgumentNullException out of MakeGenericType) for a Guid Id on a wrapper
            // snapshot document. Register() takes the already-typed projection instance directly,
            // sidestepping that lookup.
            options.Projections.Register(new MedicineScheduleSnapshotProjection(), ProjectionLifecycle.Inline);
            options.Schema.For<MedicineScheduleSnapshot>().DatabaseSchemaName(SnapshotSchema.Name);

            options.Projections.Register(new MedicineSharingSnapshotProjection(), ProjectionLifecycle.Inline);
            options.Schema.For<MedicineSharingSnapshot>().DatabaseSchemaName(SnapshotSchema.Name);

            return options;
        });

        services.AddSingleton<IMedicineEventStore, MartenMedicineEventStore>();
        services.AddSingleton<IMedicineSharingEventStore, MartenMedicineSharingEventStore>();

        return services;
    }

    public static IEndpointRouteBuilder MapMedicinesFeature(this IEndpointRouteBuilder endpoints)
    {
        var medicines = endpoints.MapGroup("/medicines")
            .WithTags("Medicines")
            .RequireAuthorization()
            .WithGroupName(OpenApiDocumentName)
            .WithETag();

        medicines.MapCreateMedicineSchedule();
        medicines.MapUpdateMedicineDetails();
        medicines.MapRescheduleMedicine();
        medicines.MapStopMedicineSchedule();
        medicines.MapListMedicineSchedules();
        medicines.MapListTodaysDoses();
        medicines.MapSetDoseStatus();

        medicines.MapShareMedicineWithGroup();
        medicines.MapUnshareMedicineFromGroup();
        medicines.MapGetSharedMedicineGroup();

        medicines.MapCreateMedicineScheduleForGroup();
        medicines.MapUpdateMedicineDetailsForGroup();
        medicines.MapRescheduleMedicineForGroup();
        medicines.MapStopMedicineScheduleForGroup();
        medicines.MapListMedicineSchedulesForGroup();
        medicines.MapListTodaysDosesForGroup();
        medicines.MapSetDoseStatusForGroup();

        return endpoints;
    }
}
