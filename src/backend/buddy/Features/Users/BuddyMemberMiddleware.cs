using buddy.Common;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace buddy.Features.Users;

// Signing in to Keycloak isn't enough to use Buddy: the account must also be in the buddy-guardian
// or buddy-child group (KeycloakGroups). A valid token without either is rejected here with
// 403 not_a_buddy_member on every endpoint that requires authorization, GET /users/me included, so
// such an account is never provisioned as a Buddy user. 403 rather than 401 for the same reason as
// ProvisionedUserMiddleware: the token is valid, and a client that refreshes on 401 would loop.
// Runs after authorization (anonymous callers are already handled) and before
// ProvisionedUserMiddleware, whose user_not_provisioned would otherwise mislead the client.
public sealed class BuddyMemberMiddleware(RequestDelegate next, IOptions<JsonOptions> jsonOptions, ILogger<BuddyMemberMiddleware> logger)
{
    public const string ErrorCode = "not_a_buddy_member";

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true
            || context.User.IsBuddyMember()
            || context.GetEndpoint() is not { } endpoint
            || endpoint.Metadata.GetMetadata<IAuthorizeData>() is null
            || endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            await next(context);
            return;
        }

        logger.NonMemberCallerRejected(context.Request.Method, context.Request.Path.Value);

        context.Response.StatusCode = StatusCodes.Status403Forbidden;

        var envelope = new ErrorEnvelope(
            ErrorCode,
            $"This account is in neither the '{KeycloakGroups.Guardian}' nor the '{KeycloakGroups.Child}' Keycloak group.",
            new Dictionary<string, string[]>(),
            context.TraceIdentifier);

        await context.Response.WriteAsJsonAsync(envelope, jsonOptions.Value.SerializerOptions, context.RequestAborted);
    }
}

public static class BuddyMemberMiddlewareExtensions
{
    public static IApplicationBuilder UseBuddyMembers(this IApplicationBuilder app) =>
        app.UseMiddleware<BuddyMemberMiddleware>();
}
