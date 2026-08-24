using buddy.Features.Users;
using buddy.Serialization;

using JasperFx.Events;

using Marten;

using Microsoft.Extensions.Options;

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

        services.Configure<PostgresOptions>(configuration.GetSection(PostgresOptions.SectionName));

        services.AddMartenStore<IMedicinesStore>(serviceProvider =>
        {
            var postgres = serviceProvider.GetRequiredService<IOptionsMonitor<PostgresOptions>>().CurrentValue;

            var options = new StoreOptions();
            options.Connection(postgres.Postgres);
            options.DatabaseSchemaName = "medicines";
            options.Events.StreamIdentity = StreamIdentity.AsGuid;
            options.Events.AddEventTypes(EventTypes);

            options.UseSystemTextJsonForSerialization(
                enumStorage: EnumStorage.AsString,
                configure: json => json.Converters.Add(new StronglyTypedIdJsonConverterFactory()));

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
            .WithGroupName(OpenApiDocumentName);

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
