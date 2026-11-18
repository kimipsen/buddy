using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace buddy.Features.Mcp;

// The MCP server is for guardians: a child's account never reaches provisioning or a tool through it.
// A principal in the buddy-child group (and not buddy-guardian) gets 403 not_a_buddy_guardian here.
// Runs after BuddyMemberMiddleware (an account in neither group has already got not_a_buddy_member)
// and before ProvisionedUserMiddleware, only for paths under /mcp (Program.cs: UseWhen).
// Like the two middlewares around it, it acts only when the matched endpoint carries authorization
// metadata: POST /mcp keeps .RequireAuthorization() explicitly, and the child and not-provisioned
// tests prove that metadata is present.
public sealed class McpGuardianMiddleware(RequestDelegate next, IOptions<JsonOptions> jsonOptions, ILogger<McpGuardianMiddleware> logger)
{
    public const string ErrorCode = "not_a_buddy_guardian";

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true
            || context.User.HasClaim(KeycloakGroups.ClaimType, KeycloakGroups.Guardian)
            || context.GetEndpoint() is not { } endpoint
            || endpoint.Metadata.GetMetadata<IAuthorizeData>() is null
            || endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            await next(context);
            return;
        }

        logger.NonGuardianCallerRejected(context.Request.Method, context.Request.Path.Value);

        context.Response.StatusCode = StatusCodes.Status403Forbidden;

        var envelope = new ErrorEnvelope(
            ErrorCode,
            $"The MCP server is for guardians: this account is not in the '{KeycloakGroups.Guardian}' Keycloak group.",
            new Dictionary<string, string[]>(),
            context.TraceIdentifier);

        await context.Response.WriteAsJsonAsync(envelope, jsonOptions.Value.SerializerOptions, context.RequestAborted);
    }
}

public static class McpGuardianMiddlewareExtensions
{
    public static IApplicationBuilder UseMcpGuardians(this IApplicationBuilder app) =>
        app.UseWhen(
            context => McpAuth.IsMcpPath(context.Request.Path),
            branch => branch.UseMiddleware<McpGuardianMiddleware>());
}
