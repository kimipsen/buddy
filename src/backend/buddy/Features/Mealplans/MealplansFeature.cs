using buddy.Common.Configuration;
using buddy.Common.DataProtection;
using buddy.Common.Erasure;
using buddy.Common.Http;
using buddy.Common.Postgres;
using buddy.Serialization;

using JasperFx.Events;
using JasperFx.Events.Projections;

using Marten;

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
        typeof(AiSessionExpired)
    ];

    // Depends on IGuardianLinkEventStore for authorization, so AddGuardiansFeature must run first
    // -- same DI ordering constraint Calendars/Medicines already have relative to Guardians.
    public static IServiceCollection AddMealplansFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOpenApi(OpenApiDocumentName, options =>
        {
            options.ShouldInclude = api => api.GroupName == OpenApiDocumentName;
        });

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

        return services;
    }

    public static IEndpointRouteBuilder MapMealplansFeature(this IEndpointRouteBuilder endpoints)
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
        mealplans.MapPreviewMealPlanImport();
        mealplans.MapCommitMealPlanImport();
        mealplans.MapListMealPlanImports();
        mealplans.MapRevertMealPlanImport();
        mealplans.MapPreviewMealPlanImportForGroup();
        mealplans.MapCommitMealPlanImportForGroup();
        mealplans.MapListMealPlanImportsForGroup();
        mealplans.MapRevertMealPlanImportForGroup();

        // AI assistant: BYOK provider credentials + the chat/tool-calling session loop (see
        // docs/backend/plans -- AI-Assisted Mealplan Generation). OpenAi/Gemini and the calendar
        // tool land in later phases.
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

        return endpoints;
    }
}
