using buddy.Features.Users;
using buddy.Serialization;

using JasperFx.Events;

using Marten;

using Microsoft.Extensions.Options;

using Weasel.Core;

namespace buddy.Features.Mealplans;

public static class MealplansFeature
{
    public const string OpenApiDocumentName = "mealplans";

    private static readonly Type[] EventTypes =
    [
        typeof(MealCreated),
        typeof(MealDetailsUpdated),
        typeof(MealArchived),
        typeof(MealRated),
        typeof(MealPlanCreated),
        typeof(MealAssignedToSlot),
        typeof(MealSlotCleared)
    ];

    // Depends on IGuardianLinkEventStore for authorization, so AddGuardiansFeature must run first
    // -- same DI ordering constraint Calendars/Medicines already have relative to Guardians.
    public static IServiceCollection AddMealplansFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOpenApi(OpenApiDocumentName, options =>
        {
            options.ShouldInclude = api => api.GroupName == OpenApiDocumentName;
        });

        services.Configure<PostgresOptions>(configuration.GetSection(PostgresOptions.SectionName));

        services.AddMartenStore<IMealplansStore>(serviceProvider =>
        {
            var postgres = serviceProvider.GetRequiredService<IOptionsMonitor<PostgresOptions>>().CurrentValue;

            var options = new StoreOptions();
            options.Connection(postgres.Postgres);
            options.DatabaseSchemaName = "mealplans";
            options.Events.StreamIdentity = StreamIdentity.AsGuid;
            options.Events.AddEventTypes(EventTypes);

            options.UseSystemTextJsonForSerialization(
                enumStorage: EnumStorage.AsString,
                configure: json => json.Converters.Add(new StronglyTypedIdJsonConverterFactory()));

            return options;
        });

        services.AddSingleton<IMealEventStore, MartenMealEventStore>();
        services.AddSingleton<IMealPlanEventStore, MartenMealPlanEventStore>();

        return services;
    }

    public static IEndpointRouteBuilder MapMealplansFeature(this IEndpointRouteBuilder endpoints)
    {
        var mealplans = endpoints.MapGroup("/mealplans")
            .WithTags("Mealplans")
            .RequireAuthorization()
            .WithGroupName(OpenApiDocumentName);

        mealplans.MapCreateMeal();
        mealplans.MapUpdateMealDetails();
        mealplans.MapArchiveMeal();
        mealplans.MapListMeals();
        mealplans.MapRateMeal();
        mealplans.MapAssignMealToSlot();
        mealplans.MapClearMealSlot();
        mealplans.MapListMealPlan();

        return endpoints;
    }
}
