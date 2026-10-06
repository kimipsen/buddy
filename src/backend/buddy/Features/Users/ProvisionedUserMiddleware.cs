using buddy.Common;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace buddy.Features.Users;

// A valid Keycloak token whose subject has no Buddy user yet (it hasn't called GET /users/me,
// GetOrCreateUser) carries no Claims.UserId -- UserIdClaimsTransformation skips it. Rejecting that
// caller here, once, with 403 user_not_provisioned is what lets every command take a non-null
// UserId (Claims.GetRequiredUserId) instead of each handler guarding for it. 403 rather than 401:
// the token is valid, so a client that refreshes on 401 would loop. Runs after authorization, so
// anonymous callers have already been handled. Only endpoints that require authorization are
// checked: public ones (iCal feeds, invite previews) and the provisioning endpoint itself
// (AllowUnprovisionedUser) pass through.
// See docs/backend/analysis/eliminate-nulls.md, Phase 1.
public sealed class ProvisionedUserMiddleware(RequestDelegate next, IOptions<JsonOptions> jsonOptions, ILogger<ProvisionedUserMiddleware> logger)
{
    public const string ErrorCode = "user_not_provisioned";

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true
            || context.User.GetUserId() is not null
            || context.GetEndpoint() is not { } endpoint
            || endpoint.Metadata.GetMetadata<IAuthorizeData>() is null
            || endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null
            || endpoint.Metadata.GetMetadata<AllowUnprovisionedUserMetadata>() is not null)
        {
            await next(context);
            return;
        }

        logger.UnprovisionedCallerRejected(context.Request.Method, context.Request.Path);

        context.Response.StatusCode = StatusCodes.Status403Forbidden;

        var envelope = new ErrorEnvelope(
            ErrorCode,
            "No Buddy user exists for this account yet. Call GET /users/me first.",
            new Dictionary<string, string[]>(),
            context.TraceIdentifier);

        await context.Response.WriteAsJsonAsync(envelope, jsonOptions.Value.SerializerOptions, context.RequestAborted);
    }
}

public sealed class AllowUnprovisionedUserMetadata;

public static class ProvisionedUserMiddlewareExtensions
{
    public static IApplicationBuilder UseProvisionedUsers(this IApplicationBuilder app) =>
        app.UseMiddleware<ProvisionedUserMiddleware>();

    // Only for the endpoint that creates the Buddy user (GET /users/me).
    public static TBuilder AllowUnprovisionedUser<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(new AllowUnprovisionedUserMetadata());
}
