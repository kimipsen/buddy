using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace buddy.Features.Users;

public static class UsersFeature
{
    public static IServiceCollection AddUsersFeature(this IServiceCollection services, IConfiguration configuration)
    {
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
        services.AddSingleton<IUserEventStore, FileUserEventStore>();
        services.AddSingleton<UserService>();

        return services;
    }

    public static IEndpointRouteBuilder MapUsersFeature(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/users/me", async (
            ClaimsPrincipal principal,
            UserService users,
            CancellationToken cancellationToken) =>
        {
            var user = await users.GetOrCreateFromClaimsAsync(principal, cancellationToken);

            return Results.Ok(new UserResponse(
                user.Id,
                user.KeycloakSubject,
                user.Email,
                user.UserName,
                user.DisplayName));
        })
        .RequireAuthorization()
        .WithName("GetCurrentUser");

        return endpoints;
    }
}
