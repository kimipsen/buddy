using System.Net.Sockets;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

using Npgsql;

namespace buddy.Common.Errors;

// The last line of defence: an exception nothing else handled becomes the same ErrorEnvelope every
// other error uses (docs/backend/http-status-codes.md), never the framework's empty 500 or a stack
// trace. The more specific middleware (RequestBindingFailureMiddleware -> 400,
// ConcurrencyConflictMiddleware -> 409) still catch their own exceptions first.
//
//   503 dependency_unavailable  Postgres, Keycloak or the SMTP server couldn't be reached. The
//                               request may well work again shortly, so Retry-After is set.
//   500 internal_error          Anything else: a bug, or data the code didn't expect.
//
// The body never carries the exception's message or type: requestId is how a report is matched to
// the logged exception (and its trace). Logging is the exception handler middleware's own: an Error
// entry under Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware for every exception, and
// the exception type as error.type on the request metrics. An exception from a Wolverine handler
// also gets Wolverine's own Error entry, under the command's name, with the same TraceId.
public static class ExceptionHandlingFeature
{
    public const string InternalErrorCode = "internal_error";
    public const string DependencyUnavailableCode = "dependency_unavailable";

    public static readonly TimeSpan RetryAfter = TimeSpan.FromSeconds(5);

    public static IServiceCollection AddExceptionHandlingFeature(this IServiceCollection services)
    {
        services.AddExceptionHandler<UnhandledExceptionHandler>();

        // The fallback writer UseExceptionHandler needs when no handler takes an exception. Ours
        // always does, so clients only ever see the ErrorEnvelope.
        services.AddProblemDetails();

        return services;
    }

    public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder app) =>
        app.UseExceptionHandler(new ExceptionHandlerOptions
        {
            // Since .NET 10 the middleware skips its own logging and diagnostics when an
            // IExceptionHandler handles the exception. Keep them: they're the only record of it.
            SuppressDiagnosticsCallback = _ => false
        });

    internal static bool IsDependencyUnavailable(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is NpgsqlException { IsTransient: true }
                or SocketException
                or HttpRequestException { HttpRequestError: HttpRequestError.ConnectionError or HttpRequestError.NameResolutionError })
            {
                return true;
            }
        }

        return false;
    }
}

public sealed class UnhandledExceptionHandler(IOptions<JsonOptions> jsonOptions) : IExceptionHandler
{
    private static readonly IReadOnlyDictionary<string, string[]> NoDetails = new Dictionary<string, string[]>();

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        ErrorEnvelope envelope;

        if (ExceptionHandlingFeature.IsDependencyUnavailable(exception))
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.Headers.RetryAfter = ((int)ExceptionHandlingFeature.RetryAfter.TotalSeconds).ToString();
            envelope = new ErrorEnvelope(
                ExceptionHandlingFeature.DependencyUnavailableCode,
                "A service Buddy depends on is temporarily unavailable. Please try again shortly.",
                NoDetails,
                context.TraceIdentifier);
        }
        else
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            envelope = new ErrorEnvelope(
                ExceptionHandlingFeature.InternalErrorCode,
                "Something went wrong on our side.",
                NoDetails,
                context.TraceIdentifier);
        }

        await context.Response.WriteAsJsonAsync(envelope, jsonOptions.Value.SerializerOptions, cancellationToken);
        return true;
    }
}
