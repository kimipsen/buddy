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

namespace buddy.Features.FlashCards;

public static class FlashCardsFeature
{
    public const string OpenApiDocumentName = "flashcards";

    private static readonly Type[] EventTypes =
    [
        typeof(FlashCardSetCreated),
        typeof(FlashCardSetUpdated),
        typeof(FlashCardAdded),
        typeof(FlashCardUpdated),
        typeof(FlashCardRemoved),
        typeof(FlashCardSetDeleted),
        typeof(FlashCardPracticeStarted),
        typeof(FlashCardSetEnabled),
        typeof(FlashCardSetDisabled),
        typeof(FlashCardGameFinished)
    ];

    // Registration is unconditional: the stores, event types and the privacy exporter/eraser exist
    // with the FlashCards flag off. The flag gates only the routes (MapFlashCardsFeature) and the UI.
    // The "flashcards" schema is created by Marten's default CreateOrUpdate on startup, like every
    // other feature's schema; there is no migration step.
    public static IServiceCollection AddFlashCardsFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddBuddyOpenApiDocument(OpenApiDocumentName);

        // Shared process-wide pool -- see PostgresDataSource.
        services.AddPostgresDataSource(configuration);
        // Erases this feature's part of a person (docs/backend/analysis/gdpr-data-protection.md).
        services.AddSingleton<IPersonalDataEraser, FlashCardsPersonalDataEraser>();
        services.AddSingleton<IPersonalDataExporter, FlashCardsPersonalDataExporter>();

        services.AddMartenStore<IFlashCardsStore>(serviceProvider =>
        {
            var options = new StoreOptions();
            options.Connection(serviceProvider.GetRequiredService<NpgsqlDataSource>());
            options.DatabaseSchemaName = "flashcards";
            options.Events.StreamIdentity = StreamIdentity.AsGuid;
            options.Events.AddEventTypes(EventTypes);

            options.UseSystemTextJsonForSerialization(
                enumStorage: EnumStorage.AsString,
                configure: json => json.Converters.Add(new StronglyTypedIdJsonConverterFactory()));

            // Inline snapshots, kept transactionally consistent with every append and routed to the
            // shared "snapshots" schema: derived, rebuildable read state. Register(), not
            // Projections.Snapshot<T>() (see PickupsFeature).
            options.Projections.Register(new FlashCardSetSnapshotProjection(), ProjectionLifecycle.Inline);
            options.Schema.For<FlashCardSetSnapshot>().DatabaseSchemaName(SnapshotSchema.Name);

            options.Projections.Register(new FlashCardPracticeSnapshotProjection(), ProjectionLifecycle.Inline);
            options.Schema.For<FlashCardPracticeSnapshot>().DatabaseSchemaName(SnapshotSchema.Name);

            return options;
        });

        services.AddSingleton<FlashCardsMetrics>();
        // Decks are shuffled with the shared thread-safe instance.
        services.AddSingleton<Random>(_ => Random.Shared);

        services.AddSingleton<IFlashCardSetEventStore, MartenFlashCardSetEventStore>();
        services.AddSingleton<IFlashCardPracticeEventStore, MartenFlashCardPracticeEventStore>();

        return services;
    }

    public static IEndpointRouteBuilder MapFlashCardsFeature(this IEndpointRouteBuilder endpoints)
    {
        var flashCards = endpoints.MapGroup("/flash-cards")
            .WithTags("FlashCards")
            .RequireAuthorization()
            .WithGroupName(OpenApiDocumentName)
            .WithETag();

        flashCards.MapListFlashCardSets();
        flashCards.MapCreateFlashCardSet();
        flashCards.MapUpdateFlashCardSet();
        flashCards.MapDeleteFlashCardSet();
        flashCards.MapAddFlashCard();
        flashCards.MapUpdateFlashCard();
        flashCards.MapRemoveFlashCard();
        flashCards.MapGetChildFlashCards();
        flashCards.MapEnableFlashCardSet();
        flashCards.MapDisableFlashCardSet();
        flashCards.MapListMyFlashCardSets();
        flashCards.MapGetFlashCardDeck();
        flashCards.MapFinishFlashCardGame();

        return endpoints;
    }
}
