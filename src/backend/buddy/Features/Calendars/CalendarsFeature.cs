using buddy.Features.Users;
using buddy.Serialization;

using JasperFx.Events;

using Marten;

using Microsoft.Extensions.Options;

using Weasel.Core;

namespace buddy.Features.Calendars;

public static class CalendarsFeature
{
    public const string OpenApiDocumentName = "calendars";

    private static readonly Type[] EventTypes =
    [
        typeof(CalendarCreated),
        typeof(CalendarCreatedForGroup),
        typeof(CalendarTransferredToGroup),
        typeof(CalendarDeleted),
        typeof(MemberRoleGranted),
        typeof(MemberRoleRevoked),
        typeof(EventItemCreated),
        typeof(TaskItemCreated),
        typeof(ItemDetailsUpdated),
        typeof(EventRescheduled),
        typeof(TaskRescheduled),
        typeof(RecurrenceUpdated),
        typeof(ItemDeleted),
        typeof(IcalTokenIssued),
        typeof(IcalTokenRevoked)
    ];

    public static IServiceCollection AddCalendarsFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOpenApi(OpenApiDocumentName, options =>
        {
            options.ShouldInclude = api => api.GroupName == OpenApiDocumentName;
        });

        services.Configure<PostgresOptions>(configuration.GetSection(PostgresOptions.SectionName));

        services.AddMartenStore<ICalendarsStore>(serviceProvider =>
        {
            var postgres = serviceProvider.GetRequiredService<IOptionsMonitor<PostgresOptions>>().CurrentValue;

            var options = new StoreOptions();
            options.Connection(postgres.Postgres);
            options.DatabaseSchemaName = "calendars";
            options.Events.StreamIdentity = StreamIdentity.AsGuid;
            options.Events.AddEventTypes(EventTypes);

            options.UseSystemTextJsonForSerialization(
                enumStorage: EnumStorage.AsString,
                configure: json => json.Converters.Add(new StronglyTypedIdJsonConverterFactory()));

            return options;
        });

        services.AddSingleton<ICalendarEventStore, MartenCalendarEventStore>();
        services.AddSingleton<ICalendarItemEventStore, MartenCalendarItemEventStore>();

        return services;
    }

    public static IEndpointRouteBuilder MapCalendarsFeature(this IEndpointRouteBuilder endpoints)
    {
        var calendars = endpoints.MapGroup("/calendars")
            .WithTags("Calendars")
            .RequireAuthorization()
            .WithGroupName(OpenApiDocumentName);

        calendars.MapCreateCalendar();
        calendars.MapGetCalendar();
        calendars.MapListCalendars();
        calendars.MapDeleteCalendar();
        calendars.MapTransferCalendarToGroup();
        calendars.MapSetMemberRole();
        calendars.MapRemoveMember();
        calendars.MapCreateItem();
        calendars.MapListItems();
        calendars.MapListOccurrences();
        calendars.MapUpdateItemDetails();
        calendars.MapRescheduleItem();
        calendars.MapUpdateItemRecurrence();
        calendars.MapDeleteItem();
        calendars.MapCreateIcalToken();
        calendars.MapListIcalTokens();
        calendars.MapRevokeIcalToken();
        calendars.MapGetIcalFeed();

        return endpoints;
    }
}
