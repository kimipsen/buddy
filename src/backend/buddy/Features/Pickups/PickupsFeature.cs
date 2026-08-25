using buddy.Features.Users;
using buddy.Serialization;

using JasperFx.Events;

using Marten;

using Microsoft.Extensions.Options;

using Weasel.Core;

namespace buddy.Features.Pickups;

public static class PickupsFeature
{
    public const string OpenApiDocumentName = "pickups";

    private static readonly Type[] EventTypes =
    [
        typeof(PickupScheduleCreated),
        typeof(PickupAssigned),
        typeof(PickupCleared)
    ];

    // Depends on IGuardianLinkEventStore for authorization, so AddGuardiansFeature must run first
    // -- same DI ordering constraint Calendars/Medicines/Mealplans already have relative to
    // Guardians.
    public static IServiceCollection AddPickupsFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOpenApi(OpenApiDocumentName, options =>
        {
            options.ShouldInclude = api => api.GroupName == OpenApiDocumentName;
        });

        services.Configure<PostgresOptions>(configuration.GetSection(PostgresOptions.SectionName));

        services.AddMartenStore<IPickupsStore>(serviceProvider =>
        {
            var postgres = serviceProvider.GetRequiredService<IOptionsMonitor<PostgresOptions>>().CurrentValue;

            var options = new StoreOptions();
            options.Connection(postgres.Postgres);
            options.DatabaseSchemaName = "pickups";
            options.Events.StreamIdentity = StreamIdentity.AsGuid;
            options.Events.AddEventTypes(EventTypes);

            options.UseSystemTextJsonForSerialization(
                enumStorage: EnumStorage.AsString,
                configure: json => json.Converters.Add(new StronglyTypedIdJsonConverterFactory()));

            return options;
        });

        services.AddSingleton<IPickupScheduleEventStore, MartenPickupScheduleEventStore>();

        return services;
    }

    public static IEndpointRouteBuilder MapPickupsFeature(this IEndpointRouteBuilder endpoints)
    {
        var pickups = endpoints.MapGroup("/pickups")
            .WithTags("Pickups")
            .RequireAuthorization()
            .WithGroupName(OpenApiDocumentName);

        pickups.MapAssignPickup();
        pickups.MapClearPickup();
        pickups.MapListPickupSchedule();

        return endpoints;
    }
}
