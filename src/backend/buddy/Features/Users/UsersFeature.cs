using System.Security.Claims;

using buddy.Features.Guardians;
using buddy.Serialization;

using JasperFx.Events;

using Marten;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using Weasel.Core;

namespace buddy.Features.Users;

public static class UsersFeature
{
    public const string OpenApiDocumentName = "users";

    private static readonly Type[] EventTypes =
    [
        typeof(UserCreated),
        typeof(UserDeleted),
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

        services.Configure<KeycloakOptions>(configuration.GetSection(KeycloakOptions.SectionName));
        services.Configure<PostgresOptions>(configuration.GetSection(PostgresOptions.SectionName));

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
            var postgres = serviceProvider.GetRequiredService<IOptionsMonitor<PostgresOptions>>().CurrentValue;

            var options = new StoreOptions();
            options.Connection(postgres.Postgres);
            options.DatabaseSchemaName = "users";
            options.Events.StreamIdentity = StreamIdentity.AsGuid;
            options.Events.AddEventTypes(EventTypes);

            options.UseSystemTextJsonForSerialization(
                enumStorage: EnumStorage.AsString,
                configure: json => json.Converters.Add(new StronglyTypedIdJsonConverterFactory()));

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
            .WithGroupName(OpenApiDocumentName);

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
