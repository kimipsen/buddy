using buddy.Common.Erasure;
using buddy.Common.Http;
using buddy.Common.Postgres;
using buddy.Serialization;

using JasperFx.Events;
using JasperFx.Events.Projections;

using Marten;

using Npgsql;

using Weasel.Core;

namespace buddy.Features.Calendars;

public static class CalendarsFeature
{
    public const string OpenApiDocumentName = "calendars";

    private static readonly Type[] EventTypes =
    [
        typeof(CalendarCreatedForGroup),
        typeof(CalendarIconChanged),
        typeof(CalendarTransferredToGroup),
        typeof(CalendarDeleted),
        typeof(MemberRoleGranted),
        typeof(MemberRoleRevoked),
        typeof(EventItemCreated),
        typeof(TaskItemCreated),
        typeof(TemplateTaskItemCreated),
        typeof(ItemDetailsUpdated),
        typeof(EventRescheduled),
        typeof(TaskRescheduled),
        typeof(RecurrenceUpdated),
        typeof(TaskCompletionChanged),
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

        // Shared process-wide pool -- see PostgresDataSource.
        services.AddPostgresDataSource(configuration);
        // Erases this feature's part of a person (docs/backend/analysis/gdpr-data-protection.md).
        services.AddSingleton<IPersonalDataEraser, CalendarsPersonalDataEraser>();

        services.AddMartenStore<ICalendarsStore>(serviceProvider =>
        {
            var options = new StoreOptions();
            options.Connection(serviceProvider.GetRequiredService<NpgsqlDataSource>());
            options.DatabaseSchemaName = "calendars";
            options.Events.StreamIdentity = StreamIdentity.AsGuid;
            options.Events.AddEventTypes(EventTypes);

            // What erasure masks in the streams this store keeps (gdpr-data-protection.md).
            CalendarsPersonalDataEraser.ConfigureMasking(options);

            options.UseSystemTextJsonForSerialization(
                enumStorage: EnumStorage.AsString,
                configure: json =>
                {
                    json.Converters.Add(new StronglyTypedIdJsonConverterFactory());

                    // CompletionTarget (TaskCompletionChanged, CalendarItem.CompletionLog) needs an
                    // explicit Kind discriminator -- see CompletionTargetJsonConverter.
                    json.Converters.Add(new CompletionTargetJsonConverter());

                    // Recurrence (the creation events, RecurrenceUpdated, CalendarItem.Recurrence)
                    // needs explicit Kind discriminators -- see RecurrenceJsonConverter.
                    json.Converters.Add(new RecurrenceJsonConverter());

                    // CalendarItem.Schedule (ItemSchedule) needs an explicit Kind discriminator to
                    // round-trip through the snapshot -- see ItemScheduleJsonConverter.
                    json.Converters.Add(new ItemScheduleJsonConverter());
                });

            // Inline snapshots of Calendar and CalendarItem, kept transactionally consistent with
            // every event append. Routed to a schema separate from "calendars" -- they're
            // derived/rebuildable read state, never the source of truth. See
            // docs/backend/analysis/event-stream-snapshots.md.
            //
            // Registered explicitly via Register(), not the Projections.Snapshot<T>() convenience
            // method: that method tries to auto-derive the document's TId via reflection, which
            // throws (ArgumentNullException out of MakeGenericType) for a Guid Id on a wrapper
            // record. Register() takes the already-typed projection instance directly,
            // sidestepping that lookup.
            options.Projections.Register(new CalendarSnapshotProjection(), ProjectionLifecycle.Inline);
            options.Schema.For<CalendarSnapshot>().DatabaseSchemaName(SnapshotSchema.Name);

            options.Projections.Register(new CalendarItemSnapshotProjection(), ProjectionLifecycle.Inline);
            options.Schema.For<CalendarItemSnapshot>().DatabaseSchemaName(SnapshotSchema.Name);

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
            .WithGroupName(OpenApiDocumentName)
            .WithETag();

        calendars.MapCreateCalendar();
        calendars.MapGetCalendar();
        calendars.MapUpdateCalendarIcon();
        calendars.MapListCalendars();
        calendars.MapDeleteCalendar();
        calendars.MapTransferCalendarToGroup();
        calendars.MapSetMemberRole();
        calendars.MapRemoveMember();
        calendars.MapCreateItem();
        calendars.MapScheduleTaskFromTemplate();
        calendars.MapListItems();
        calendars.MapListOccurrences();
        calendars.MapListAssignableMembers();
        calendars.MapUpdateItemDetails();
        calendars.MapRescheduleItem();
        calendars.MapUpdateItemRecurrence();
        calendars.MapSetTaskCompletion();
        calendars.MapDeleteItem();
        calendars.MapCreateIcalToken();
        calendars.MapListIcalTokens();
        calendars.MapRevokeIcalToken();
        calendars.MapGetIcalFeed();

        return endpoints;
    }
}
