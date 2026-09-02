using System.Security.Cryptography;
using System.Text;

using buddy.Features.Users;

using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace buddy.Common.Idempotency;

// Opt-in retry safety for POST endpoints that create a resource (CreateMeal, CreateGroup,
// CreateChild, InviteGuardian, ...): with no Idempotency-Key header, behavior is unchanged from
// today. With one, a repeated POST with the same key (a network-timeout retry, a double-tap)
// replays the first call's exact response instead of creating a second resource. This is
// deliberately generic HTTP middleware rather than per-handler changes -- DELETE/PUT/PATCH are
// already idempotent by construction (see the handler-level before/after checks throughout this
// codebase), so only POST needs it, and a single middleware covers all of it without touching
// each Create*/Invite*/Accept* handler individually.
public sealed class IdempotencyKeyMiddleware(RequestDelegate next, IdempotencyKeyRepository repository, IOptions<JsonOptions> jsonOptions)
{
    public const string HeaderName = "Idempotency-Key";
    private const int MaxKeyLength = 200;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!HttpMethods.IsPost(context.Request.Method) || !context.Request.Headers.TryGetValue(HeaderName, out var keyValues))
        {
            await next(context);
            return;
        }

        var key = keyValues.ToString();

        if (string.IsNullOrWhiteSpace(key) || key.Length > MaxKeyLength)
        {
            await WriteErrorAsync(context, StatusCodes.Status400BadRequest, "invalid_idempotency_key",
                $"{HeaderName} must be a non-empty string of at most {MaxKeyLength} characters.");
            return;
        }

        // No backend UserId yet means this can't be a resource-creating call in the first place
        // (every mutating command requires one) -- fall through rather than keying a cache entry
        // on nothing.
        if (context.User.GetUserId() is not { } userId)
        {
            await next(context);
            return;
        }

        var cancellationToken = context.RequestAborted;
        var fingerprint = await ComputeFingerprintAsync(context.Request);

        var existing = await repository.FindAsync(userId.Value, key, cancellationToken);

        if (existing is not null)
        {
            await HandleExistingAsync(context, existing, fingerprint, cancellationToken);
            return;
        }

        if (!await repository.TryReserveAsync(userId.Value, key, fingerprint, cancellationToken))
        {
            // Lost the race to claim the key -- re-read and treat it exactly like an
            // already-existing record found above (this covers both "it finished between our
            // FindAsync and our TryReserveAsync" and "it's still in flight").
            var winner = await repository.FindAsync(userId.Value, key, cancellationToken);

            if (winner is null)
            {
                await WriteConflictAsync(context, "idempotency_key_in_progress", "A request with this Idempotency-Key is already being processed.");
                return;
            }

            await HandleExistingAsync(context, winner, fingerprint, cancellationToken);
            return;
        }

        var originalBody = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await next(context);
        }
        catch
        {
            context.Response.Body = originalBody;
            await repository.ReleaseAsync(userId.Value, key, cancellationToken);
            throw;
        }

        context.Response.Body = originalBody;
        var responseBytes = buffer.ToArray();

        await repository.CompleteAsync(userId.Value, key, context.Response.StatusCode, context.Response.ContentType, responseBytes, cancellationToken);

        if (responseBytes.Length > 0)
        {
            await context.Response.Body.WriteAsync(responseBytes, cancellationToken);
        }
    }

    private async Task HandleExistingAsync(HttpContext context, IdempotencyRecord record, string fingerprint, CancellationToken cancellationToken)
    {
        if (record.RequestFingerprint != fingerprint)
        {
            await WriteConflictAsync(context, "idempotency_key_reused", $"This {HeaderName} was already used with a different request.");
            return;
        }

        if (record.Status == IdempotencyStatus.InProgress)
        {
            await WriteConflictAsync(context, "idempotency_key_in_progress", "A request with this Idempotency-Key is already being processed.");
            return;
        }

        context.Response.StatusCode = record.ResponseStatusCode ?? StatusCodes.Status200OK;

        if (record.ResponseContentType is { } contentType)
        {
            context.Response.ContentType = contentType;
        }

        if (record.ResponseBody is { Length: > 0 } body)
        {
            await context.Response.Body.WriteAsync(body, cancellationToken);
        }
    }

    private Task WriteConflictAsync(HttpContext context, string code, string message) =>
        WriteErrorAsync(context, StatusCodes.Status409Conflict, code, message);

    private async Task WriteErrorAsync(HttpContext context, int statusCode, string code, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var envelope = new ErrorEnvelope(code, message, new Dictionary<string, string[]>(), context.TraceIdentifier);
        await context.Response.WriteAsJsonAsync(envelope, jsonOptions.Value.SerializerOptions, context.RequestAborted);
    }

    // Method + path + query + body: the same Idempotency-Key replayed against a materially
    // different request (a client bug, or a key collision) is rejected rather than silently
    // replaying the wrong response.
    private static async Task<string> ComputeFingerprintAsync(HttpRequest request)
    {
        request.EnableBuffering();
        request.Body.Position = 0;

        using var bodyBuffer = new MemoryStream();
        await request.Body.CopyToAsync(bodyBuffer);
        request.Body.Position = 0;

        using var combined = new MemoryStream();
        var prefix = Encoding.UTF8.GetBytes($"{request.Method} {request.Path}{request.QueryString}\n");
        await combined.WriteAsync(prefix);
        bodyBuffer.Position = 0;
        await bodyBuffer.CopyToAsync(combined);

        return Convert.ToHexString(SHA256.HashData(combined.ToArray()));
    }
}
