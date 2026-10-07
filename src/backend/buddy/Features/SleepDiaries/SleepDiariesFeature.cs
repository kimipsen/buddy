using buddy.Common.Erasure;
using buddy.Common.Http;
using buddy.Common.Postgres;
using buddy.Serialization;

using JasperFx.Events;
using JasperFx.Events.Projections;

using Marten;

using Npgsql;

using Weasel.Core;

namespace buddy.Features.SleepDiaries;

public static class SleepDiariesFeature
{
    public const string OpenApiDocumentName = "sleepdiaries";

    private static readonly Type[] EventTypes =
    [
        typeof(SleepDiaryStarted),
        typeof(SleepEntryLogged),
        typeof(SleepEntryCleared),
        typeof(SleepHygieneNotesUpdated),
        typeof(SleepDiaryShareTokenCreated),
        typeof(SleepDiaryShareTokenRevoked)
    ];

    // Depends on IGuardianLinkEventStore for authorization and IUserEventStore for the child's name
    // on the shared view, so AddUsersFeature and AddGuardiansFeature must run first.
    public static IServiceCollection AddSleepDiariesFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOpenApi(OpenApiDocumentName, options =>
        {
            options.ShouldInclude = api => api.GroupName == OpenApiDocumentName;
        });

        // Shared process-wide pool -- see PostgresDataSource.
        services.AddPostgresDataSource(configuration);
        // Erases this feature's part of a person (docs/backend/analysis/gdpr-data-protection.md).
        services.AddSingleton<IPersonalDataEraser, SleepDiariesPersonalDataEraser>();

        services.AddMartenStore<ISleepDiariesStore>(serviceProvider =>
        {
            var options = new StoreOptions();
            options.Connection(serviceProvider.GetRequiredService<NpgsqlDataSource>());
            options.DatabaseSchemaName = "sleepdiaries";
            options.Events.StreamIdentity = StreamIdentity.AsGuid;
            options.Events.AddEventTypes(EventTypes);

            options.UseSystemTextJsonForSerialization(
                enumStorage: EnumStorage.AsString,
                configure: json => json.Converters.Add(new StronglyTypedIdJsonConverterFactory()));

            // Inline snapshots in the shared "snapshots" schema, registered via Register() rather
            // than Projections.Snapshot<T>() -- see PickupsFeature for why.
            options.Projections.Register(new SleepDiarySnapshotProjection(), ProjectionLifecycle.Inline);
            options.Schema.For<SleepDiarySnapshot>().DatabaseSchemaName(SnapshotSchema.Name);
            options.Projections.Register(new SleepDiaryShareTokenSnapshotProjection(), ProjectionLifecycle.Inline);
            options.Schema.For<SleepDiaryShareTokenSnapshot>().DatabaseSchemaName(SnapshotSchema.Name);

            return options;
        });

        services.AddSingleton<ISleepDiaryEventStore, MartenSleepDiaryEventStore>();
        services.AddSingleton<ISleepDiaryShareTokenEventStore, MartenSleepDiaryShareTokenEventStore>();

        return services;
    }

    public static IEndpointRouteBuilder MapSleepDiariesFeature(this IEndpointRouteBuilder endpoints)
    {
        var sleepDiary = endpoints.MapGroup("/sleep-diary")
            .WithTags("SleepDiary")
            .RequireAuthorization()
            .WithGroupName(OpenApiDocumentName)
            .WithETag();

        sleepDiary.MapLogSleepEntry();
        sleepDiary.MapClearSleepEntry();
        sleepDiary.MapUpdateSleepHygieneNotes();
        sleepDiary.MapListSleepDiaryEntries();
        sleepDiary.MapCreateSleepDiaryShareLink();
        sleepDiary.MapListSleepDiaryShareLinks();
        sleepDiary.MapRevokeSleepDiaryShareLink();
        sleepDiary.MapGetSharedSleepDiary();

        return endpoints;
    }
}
