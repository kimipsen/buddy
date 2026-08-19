using System.Security.Claims;
using buddy.Serialization;
using JasperFx.Events;
using Marten;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Weasel.Core;
using Wolverine;

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
        
        users.MapGet("/me", async (
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var user = await bus.InvokeAsync<User>(GetOrCreateUser.FromClaims(principal), cancellationToken);

            if (user.IsDeleted)
            {
                return Results.NotFound();
            }

            return Results.Ok(new UserResponse(
                user.Id,
                user.KeycloakSubject,
                user.Email,
                user.UserName,
                user.Name));
        })
        .WithName("GetCurrentUser");

        users.MapGet("/me/events", async (
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var userEvents = await bus.InvokeAsync<IReadOnlyCollection<UserEvent>>(GetUserEvents.FromClaims(principal), cancellationToken);

            return Results.Ok(userEvents.Select(e => new UserEventResponse(e.EventType, e.Value!)));
        })
        .WithName("GetCurrentUserEvents");

        users.MapDelete("/me", async (
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            await bus.InvokeAsync(DeleteUser.FromClaims(principal), cancellationToken);

            return Results.NoContent();
        })
        .WithName("DeleteCurrentUser");

        return endpoints;
    }
}
