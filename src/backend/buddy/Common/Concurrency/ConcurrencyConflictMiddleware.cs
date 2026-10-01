using JasperFx;

using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace buddy.Common.Concurrency;

// Maps a lost optimistic-concurrency race (see StreamVersionTracker) to 409 with the shared
// ErrorEnvelope instead of an unhandled 500 -- docs/backend/http-status-codes.md lists
// "optimistic concurrency or version mismatch" under 409. One middleware rather than a Result<T>
// case: the conflict surfaces as an exception from SaveChangesAsync inside any store, so no
// handler or endpoint switch has to know about it. Registered outside the idempotency
// middleware so a conflicted request releases its Idempotency-Key and can be retried with it.
// Marten throws EventStreamUnexpectedMaxEventIdException, a JasperFx.ConcurrencyException.
public sealed class ConcurrencyConflictMiddleware(RequestDelegate next, IOptions<JsonOptions> jsonOptions, ILogger<ConcurrencyConflictMiddleware> logger)
{
    public const string ErrorCode = "concurrency_conflict";

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ConcurrencyException exception) when (!context.Response.HasStarted)
        {
            logger.LogInformation(exception, "Concurrent modification on {Method} {Path}", context.Request.Method, context.Request.Path);

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status409Conflict;

            var envelope = new ErrorEnvelope(
                ErrorCode,
                "The resource was changed by another request. Reload it and try again.",
                new Dictionary<string, string[]>(),
                context.TraceIdentifier);

            await context.Response.WriteAsJsonAsync(envelope, jsonOptions.Value.SerializerOptions, context.RequestAborted);
        }
    }
}

public static class ConcurrencyConflictMiddlewareExtensions
{
    public static IApplicationBuilder UseConcurrencyConflicts(this IApplicationBuilder app) =>
        app.UseMiddleware<ConcurrencyConflictMiddleware>();
}
