using buddy.Common.Configuration;
using buddy.Common.DataProtection;
using buddy.Common.Erasure;
using buddy.Common.FeatureFlags;
using buddy.Common.Http;
using buddy.Common.OpenApi;
using buddy.Common.Postgres;
using buddy.Serialization;

using JasperFx.Events;
using JasperFx.Events.Projections;

using Marten;

using Microsoft.Extensions.Options;

using Npgsql;

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
        typeof(MealLinkedToRecipe),
        typeof(MealRecipeUnlinked),
        typeof(MealPlanCreated),
        typeof(MealAssignedToSlot),
        typeof(MealSlotCleared),
        typeof(MealPlanSharedWithGroup),
        typeof(MealPlanUnsharedFromGroup),
        typeof(MealPlanSlotTimeSet),
        typeof(MealPlanIcalTokenIssued),
        typeof(MealPlanIcalTokenRevoked),
        typeof(MealPlanEntriesImported),
        typeof(MealPlanImportReverted),
        typeof(AiCredentialsInitialized),
        typeof(ProviderApiKeySet),
        typeof(ProviderApiKeyRemoved),
        typeof(ActiveProviderChanged),
        typeof(ActiveProviderCleared),
        typeof(AiDataSharingAcknowledged),
        typeof(AiSessionStarted),
        typeof(AiUserMessageSent),
        typeof(AiToolInvocationRecorded),
        typeof(AiDraftAssignmentSet),
        typeof(AiDraftAssignmentCleared),
        typeof(AiAssistantMessageRecorded),
        typeof(AiSessionApplied),
        typeof(AiSessionDiscarded),
        typeof(AiSessionExpired),
        typeof(MealieConnectionInitialized),
        typeof(MealieConnected),
        typeof(MealieDisconnected)
    ];

    // Depends on IGuardianLinkEventStore for authorization, so AddGuardiansFeature must run first
    // -- same DI ordering constraint Calendars/Medicines already have relative to Guardians.
    public static IServiceCollection AddMealplansFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddBuddyOpenApiDocument(OpenApiDocumentName);

        // Shared process-wide pool -- see PostgresDataSource.
        services.AddPostgresDataSource(configuration);
        // Erases this feature's part of a person (docs/backend/analysis/gdpr-data-protection.md).
        services.AddSingleton<IPersonalDataEraser, MealplansPersonalDataEraser>();
        services.AddSingleton<IPersonalDataExporter, MealplansPersonalDataExporter>();

        services.AddMartenStore<IMealplansStore>(serviceProvider =>
        {
            var options = new StoreOptions();
            options.Connection(serviceProvider.GetRequiredService<NpgsqlDataSource>());
            options.DatabaseSchemaName = "mealplans";
            options.Events.StreamIdentity = StreamIdentity.AsGuid;
            options.Events.AddEventTypes(EventTypes);

            // What erasure masks in the streams this store keeps (gdpr-data-protection.md).
            MealplansPersonalDataEraser.ConfigureMasking(options);

            options.UseSystemTextJsonForSerialization(
                enumStorage: EnumStorage.AsString,
                configure: json =>
                {
                    json.Converters.Add(new StronglyTypedIdJsonConverterFactory());

                    // MealPlan.Assignments and MealplanAiSession.Draft are keyed by (DateOnly,
                    // MealSlot), which plain System.Text.Json can't serialize (NotSupportedException),
                    // both as a value and -- more importantly, since they're dictionary keys -- as a
                    // JSON property name. See docs/backend/analysis/event-stream-snapshots.md, gotcha 4.
                    json.Converters.Add(new ValueTupleJsonConverterFactory());
                });

            // Inline snapshots of Meal, MealPlan, AiProviderCredential and MealplanAiSession, kept
            // transactionally consistent with every event append. Routed to a schema separate from
            // "mealplans" -- they're derived/rebuildable read state, never the source of truth. See
            // docs/backend/analysis/event-stream-snapshots.md.
            //
            // Registered explicitly via Register(), not the Projections.Snapshot<T>() convenience
            // method: that method tries to auto-derive the document's TId via reflection, which
            // throws (ArgumentNullException out of MakeGenericType) for a Guid Id like these
            // snapshot wrappers use. Register() takes the already-typed projection instance
            // directly, sidestepping that lookup.
            options.Projections.Register(new MealSnapshotProjection(), ProjectionLifecycle.Inline);
            options.Schema.For<MealSnapshot>().DatabaseSchemaName(SnapshotSchema.Name);

            options.Projections.Register(new MealPlanSnapshotProjection(), ProjectionLifecycle.Inline);
            options.Schema.For<MealPlanSnapshot>().DatabaseSchemaName(SnapshotSchema.Name);

            options.Projections.Register(new AiProviderCredentialSnapshotProjection(), ProjectionLifecycle.Inline);
            options.Schema.For<AiProviderCredentialSnapshot>().DatabaseSchemaName(SnapshotSchema.Name);

            options.Projections.Register(new MealplanAiSessionSnapshotProjection(), ProjectionLifecycle.Inline);
            options.Schema.For<MealplanAiSessionSnapshot>().DatabaseSchemaName(SnapshotSchema.Name);

            options.Projections.Register(new MealieConnectionSnapshotProjection(), ProjectionLifecycle.Inline);
            options.Schema.For<MealieConnectionSnapshot>().DatabaseSchemaName(SnapshotSchema.Name);

            return options;
        });

        services.AddSingleton<IMealEventStore, MartenMealEventStore>();
        services.AddSingleton<IMealPlanEventStore, MartenMealPlanEventStore>();
        services.AddSingleton<IAiCredentialEventStore, MartenAiCredentialEventStore>();
        services.AddSingleton<IAiSessionEventStore, MartenAiSessionEventStore>();

        // 30-day retention of AI conversations (GDPR Question 6.2).
        services.AddScoped<AiSessionRetention>();
        services.AddHostedService<AiSessionRetentionService>();

        // Framework-provided at-rest encryption for stored provider API keys -- see
        // DataProtectionApiKeyCipher. The key ring is persisted in Postgres (DataProtectionFeature),
        // so stored keys stay readable across redeploys.
        services.AddDataProtectionFeature(configuration);
        services.AddSingleton<IApiKeyCipher, DataProtectionApiKeyCipher>();

        services.AddValidatedOptions<AiAssistantModelOptions>(AiAssistantModelOptions.SectionName);

        // Each is a typed HttpClient (see AddHttpClient<TClient>()'s own transient lifetime), so
        // AiProviderRegistry -- which holds all three -- stays transient too rather than becoming
        // a singleton captive dependency.
        services.AddHttpClient<AnthropicChatClient>(client => client.BaseAddress = new Uri("https://api.anthropic.com/"));
        services.AddHttpClient<OpenAiChatClient>(client => client.BaseAddress = new Uri("https://api.openai.com/"));
        services.AddHttpClient<GeminiChatClient>(client => client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/"));
        services.AddTransient<IAiProviderRegistry, AiProviderRegistry>();

        // Mealie (docs/backend/analysis/mealie-integration.md). The family's server is a URL a
        // user typed in, so its HttpClient connects only to addresses MealieOutboundPolicy allows,
        // follows no redirects and caps time and size. The base address is per family, per call.
        services.AddSingleton<IMealieConnectionEventStore, MartenMealieConnectionEventStore>();
        services.AddSingleton<IMealieTokenCipher, DataProtectionMealieTokenCipher>();
        services.AddValidatedOptions<MealieOptions>(MealieOptions.SectionName);
        services.AddMemoryCache();
        services.AddHttpClient<IMealieClient, MealieClient>(MealieClient.HttpClientName, (serviceProvider, client) =>
            {
                var mealie = serviceProvider.GetRequiredService<IOptions<MealieOptions>>().Value;
                client.Timeout = TimeSpan.FromSeconds(mealie.TimeoutSeconds);
                client.MaxResponseContentBufferSize = mealie.MaxResponseBytes;
            })
            .ConfigurePrimaryHttpMessageHandler(serviceProvider =>
            {
                var allowPrivateNetworks = serviceProvider.GetRequiredService<IOptions<MealieOptions>>().Value.AllowPrivateNetworks;

                return new SocketsHttpHandler
                {
                    AllowAutoRedirect = false,
                    UseCookies = false,
                    // Through a proxy, ConnectCallback would only ever see the proxy's address and
                    // the proxy would reach any host for us -- the guard would check nothing.
                    UseProxy = false,
                    ConnectCallback = (context, cancellationToken) => MealieOutboundPolicy.ConnectAsync(context, allowPrivateNetworks, cancellationToken)
                };
            });

        return services;
    }

    public static IEndpointRouteBuilder MapMealplansFeature(this IEndpointRouteBuilder endpoints, InstallationFeatures features)
    {
        var mealplans = endpoints.MapGroup("/mealplans")
            .WithTags("Mealplans")
            .RequireAuthorization()
            .WithGroupName(OpenApiDocumentName)
            .WithETag();

        mealplans.MapCreateMeal();
        mealplans.MapUpdateMealDetails();
        mealplans.MapArchiveMeal();
        mealplans.MapListMeals();
        mealplans.MapRateMeal();
        mealplans.MapAssignMealToSlot();
        mealplans.MapClearMealSlot();
        mealplans.MapListMealPlan();

        mealplans.MapUpdateMealSlotTimes();
        mealplans.MapCreateMealPlanIcalToken();
        mealplans.MapListMealPlanIcalTokens();
        mealplans.MapRevokeMealPlanIcalToken();
        mealplans.MapGetMealPlanIcalFeed();

        mealplans.MapShareMealPlanWithGroup();
        mealplans.MapUnshareMealPlanFromGroup();
        mealplans.MapGetSharedGroup();

        // Group-keyed siblings of the routes above -- see
        // docs/backend/analysis/group-owned-mealplans.md. RateMeal has no group-keyed sibling:
        // rating stays exclusively the child's own tier.
        mealplans.MapListMealPlanForGroup();
        mealplans.MapAssignMealToSlotForGroup();
        mealplans.MapClearMealSlotForGroup();
        mealplans.MapListMealsForGroup();
        mealplans.MapCreateMealForGroup();
        mealplans.MapUpdateMealDetailsForGroup();
        mealplans.MapArchiveMealForGroup();
        mealplans.MapGetGroupMealplanStatus();

        // Importing historical plans from notes and other systems -- see
        // docs/backend/analysis/mealplan-import.md.
        if (features.MealplanImport)
        {
            mealplans.MapPreviewMealPlanImport();
            mealplans.MapCommitMealPlanImport();
            mealplans.MapListMealPlanImports();
            mealplans.MapRevertMealPlanImport();
            mealplans.MapPreviewMealPlanImportForGroup();
            mealplans.MapCommitMealPlanImportForGroup();
            mealplans.MapListMealPlanImportsForGroup();
            mealplans.MapRevertMealPlanImportForGroup();
        }

        // AI assistant: BYOK provider credentials + the chat/tool-calling session loop (see
        // docs/backend/plans -- AI-Assisted Mealplan Generation). OpenAi/Gemini and the calendar
        // tool land in later phases.
        if (features.MealplanAiAssistant)
        {
            mealplans.MapListProviders();
            mealplans.MapSetProviderApiKey();
            mealplans.MapRemoveProviderApiKey();
            mealplans.MapSetActiveProvider();
            mealplans.MapTestProviderConnection();
            mealplans.MapGetCurrentAiSession();
            mealplans.MapAcknowledgeAiDataSharing();
            mealplans.MapStartAiSession();
            mealplans.MapSendAiSessionMessage();
            mealplans.MapApplyAiSessionDraft();
            mealplans.MapDiscardAiSession();
        }

        // Mealie: connect a family's self-hosted Mealie, link meals to recipes, import its plan,
        // fill its shopping lists -- see docs/backend/analysis/mealie-integration.md.
        if (features.MealplanMealie)
        {
            mealplans.MapGetMealieConnection();
            mealplans.MapConnectMealie();
            mealplans.MapDisconnectMealie();
            mealplans.MapTestMealieConnection();
            mealplans.MapSearchMealieRecipes();
            mealplans.MapGetMealieRecipe();
            mealplans.MapGetMealieRecipeImage();
            mealplans.MapLinkMealToRecipe();
            mealplans.MapUnlinkMealRecipe();
            mealplans.MapCreateMealFromRecipe();
            mealplans.MapListMealieShoppingLists();
            mealplans.MapAddMealPlanToMealieShoppingList();
        }

        // The Mealie import preview is an import route too, so it needs both flags.
        if (features.MealplanMealie && features.MealplanImport)
        {
            mealplans.MapPreviewMealieImport();
        }

        return endpoints;
    }
}
