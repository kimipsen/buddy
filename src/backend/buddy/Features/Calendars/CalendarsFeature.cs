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
        typeof(CalendarCreated),
        typeof(CalendarCreatedForGroup),
        typeof(CalendarIconChanged),
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

        services.AddMartenStore<ICalendarsStore>(serviceProvider =>
        {
            var options = new StoreOptions();
            options.Connection(serviceProvider.GetRequiredService<NpgsqlDataSource>());
            options.DatabaseSchemaName = "calendars";
            options.Events.StreamIdentity = StreamIdentity.AsGuid;
            options.Events.AddEventTypes(EventTypes);

            options.UseSystemTextJsonForSerialization(
                enumStorage: EnumStorage.AsString,
                configure: json =>
                {
                    json.Converters.Add(new StronglyTypedIdJsonConverterFactory());

                    // CalendarItem.CompletionLog is keyed by (DateOnly OccurrenceDate, Guid?
                    // SubtaskId) -- a System.ValueTuple dictionary key that plain
                    // System.Text.Json can't handle. Calendar itself has no tuple fields, but
                    // both snapshot documents share this one StoreOptions/serializer, so the
                    // converter is registered once here for both.
                    json.Converters.Add(new ValueTupleJsonConverterFactory());

                    // Calendar.Owner (CalendarOwner) only gets JSON-serialized now that
                    // CalendarSnapshot makes Calendar itself a stored document -- and its two
                    // cases collide under System.Text.Json's built-in union shape-based
                    // classifier (see CalendarOwnerJsonConverter for why).
                    json.Converters.Add(new CalendarOwnerJsonConverter());
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
            .WithGroupName(OpenApiDocumentName);

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
