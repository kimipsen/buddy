using System.Security.Claims;

using buddy.Common.Configuration;
using buddy.Common.Http;
using buddy.Common.Postgres;
using buddy.Features.Guardians;
using buddy.Serialization;

using JasperFx.Events;
using JasperFx.Events.Projections;

using Marten;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using Npgsql;

using Weasel.Core;

namespace buddy.Features.Users;

public static class UsersFeature
{
    public const string OpenApiDocumentName = "users";

    private static readonly Type[] EventTypes =
    [
        typeof(UserCreated),
        typeof(UserDeleted),
        typeof(UserErased),
        typeof(NameUpdated),
        typeof(EmailUpdated),
        typeof(EmailVerificationRequested),
        typeof(EmailVerified),
        typeof(TimeZoneUpdated),
        typeof(LanguageUpdated),
        // GuardianLink's stream lives in this same store/schema so a child User and its first
        // GuardianLink can be created atomically -- see MartenGuardianLinkEventStore and
        // docs/backend/analysis/child-accounts-and-guardian-roles.md. A Marten store needs every
        // CLR event type registered for any stream it will contain, regardless of which feature
        // folder declares the type.
        typeof(GuardianLinked),
        typeof(GuardianKindChanged),
        typeof(GuardianRevoked),
        // A guardian invite's own stream lives in this same store for the same reason
        // GuardianLink's does -- see MartenGuardianInviteEventStore.
        typeof(GuardianInviteCreated),
        typeof(GuardianInviteAccepted),
        typeof(GuardianInviteRevoked)
    ];

    public static IServiceCollection AddUsersFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOpenApi(OpenApiDocumentName, options =>
        {
            options.ShouldInclude = api => api.GroupName == OpenApiDocumentName;
        });

        services.AddValidatedOptions<KeycloakOptions>(KeycloakOptions.SectionName);
        // Shared process-wide pool -- see PostgresDataSource.
        services.AddPostgresDataSource(configuration);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptionsMonitor<KeycloakOptions>>((jwtBearerOptions, keycloakOptions) =>
            {
                var keycloak = keycloakOptions.CurrentValue;

                jwtBearerOptions.Authority = keycloak.Authority;
                jwtBearerOptions.Audience = keycloak.Audience;
                jwtBearerOptions.RequireHttpsMetadata = keycloak.RequireHttpsMetadata;
                jwtBearerOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = "preferred_username",
                    RoleClaimType = ClaimTypes.Role,
                    ValidateAudience = !string.IsNullOrWhiteSpace(keycloak.Audience),
                    ValidIssuer = keycloak.ValidIssuer ?? keycloak.Authority
                };
            });

        services.AddAuthorization();
        services.AddTransient<IClaimsTransformation, UserIdClaimsTransformation>();

        services.AddMartenStore<IUsersStore>(serviceProvider =>
        {
            var options = new StoreOptions();
            options.Connection(serviceProvider.GetRequiredService<NpgsqlDataSource>());
            options.DatabaseSchemaName = "users";
            options.Events.StreamIdentity = StreamIdentity.AsGuid;
            options.Events.AddEventTypes(EventTypes);

            // What erasure masks in the streams this store keeps (gdpr-data-protection.md).
            UsersPersonalData.ConfigureMasking(options);
            // Its own schema, so a restore can export it first and re-import it afterwards.
            options.Schema.For<ErasureLedgerEntry>().DatabaseSchemaName("erasure");
            GuardiansPersonalDataEraser.ConfigureMasking(options);

            options.UseSystemTextJsonForSerialization(
                enumStorage: EnumStorage.AsString,
                configure: json =>
                {
                    json.Converters.Add(new StronglyTypedIdJsonConverterFactory());

                    // User.EmailVerification needs an explicit Kind discriminator to round-trip
                    // through the snapshot -- see EmailVerificationJsonConverter.
                    json.Converters.Add(new EmailVerificationJsonConverter());
                });

            // Inline snapshots of User and GuardianLink, kept transactionally consistent with
            // every event append. Routed to a schema separate from "users" -- they're
            // derived/rebuildable read state, never the source of truth. GuardianLink is
            // registered here rather than in GuardiansFeature because its event stream lives in
            // this same Marten store/schema (see MartenGuardianLinkEventStore). See
            // docs/backend/analysis/event-stream-snapshots.md.
            //
            // Registered explicitly via Register(), not the Projections.Snapshot<T>() convenience
            // method: that method tries to auto-derive the document's TId via reflection, which
            // throws (ArgumentNullException out of MakeGenericType) for a Guid Id. Register()
            // takes the already-typed projection instance directly, sidestepping that lookup.
            options.Projections.Register(new UserSnapshotProjection(), ProjectionLifecycle.Inline);
            options.Schema.For<UserSnapshot>().DatabaseSchemaName(SnapshotSchema.Name);

            options.Projections.Register(new GuardianLinkSnapshotProjection(), ProjectionLifecycle.Inline);
            options.Schema.For<GuardianLinkSnapshot>().DatabaseSchemaName(SnapshotSchema.Name);

            return options;
        });

        services.AddSingleton<IUserEventStore, MartenUserEventStore>();

        return services;
    }

    public static IEndpointRouteBuilder MapUsersFeature(this IEndpointRouteBuilder endpoints)
    {
        var users = endpoints.MapGroup("/users")
            .WithTags("Users")
            .RequireAuthorization()
            .WithGroupName(OpenApiDocumentName)
            .WithETag();

        users.MapGetCurrentUser();
        users.MapListCurrentUserEvents();
        users.MapUpdateCurrentName();
        users.MapUpdateCurrentEmail();
        users.MapUpdateCurrentTimeZone();
        users.MapUpdateCurrentLanguage();
        users.MapResendCurrentEmailVerification();
        users.MapVerifyCurrentEmail();
        users.MapDeleteCurrentUser();

        return endpoints;
    }
}
