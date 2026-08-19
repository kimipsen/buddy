using System.Security.Claims;
using buddy.Serialization;
using JasperFx.Events;
using Marten;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
        typeof(EmailVerified)
    ];

    public static IServiceCollection AddUsersFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOpenApi(OpenApiDocumentName, options =>
        {
            options.ShouldInclude = api => api.GroupName == OpenApiDocumentName;
        });

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                var keycloak = configuration.GetSection("Authentication:Keycloak");

                options.Authority = keycloak["Authority"];
                options.Audience = keycloak["Audience"];
                options.RequireHttpsMetadata = keycloak.GetValue("RequireHttpsMetadata", true);
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = "preferred_username",
                    RoleClaimType = ClaimTypes.Role,
                    ValidateAudience = !string.IsNullOrWhiteSpace(options.Audience)
                };
            });

        services.AddAuthorization();

        services.AddMartenStore<IUsersStore>(options =>
        {
            options.Connection(configuration.GetConnectionString("Postgres")
                ?? throw new InvalidOperationException("Missing required configuration 'ConnectionStrings:Postgres'."));

            options.DatabaseSchemaName = "users";
            options.Events.StreamIdentity = StreamIdentity.AsGuid;
            options.Events.AddEventTypes(EventTypes);

            options.UseSystemTextJsonForSerialization(
                enumStorage: EnumStorage.AsString,
                configure: json => json.Converters.Add(new StronglyTypedIdJsonConverterFactory()));
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
        users.MapDeleteCurrentUser();

        return endpoints;
    }
}
