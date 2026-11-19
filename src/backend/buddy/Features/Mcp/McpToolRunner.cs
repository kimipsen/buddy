using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

using ModelContextProtocol.Protocol;

namespace buddy.Features.Mcp;

// What every tool does around its handler call: take the caller from the request, run the query,
// map the outcome (McpResults) and write the audit line (McpLog: tool name, user, outcome, duration;
// never an argument value).
public sealed class McpToolRunner(IHttpContextAccessor http, IOptions<JsonOptions> jsonOptions, TimeProvider time, ILogger<McpToolRunner> logger)
{
    public JsonSerializerOptions Json => jsonOptions.Value.SerializerOptions;

    public async Task<CallToolResult> RunAsync<T>(
        string tool, Func<ClaimsPrincipal, Task<Result<T>>> call, Func<T, object>? project = null)
    {
        var caller = Caller();
        var started = time.GetTimestamp();
        var result = McpResults.From(await call(caller), Json, project);
        Record(tool, caller, result, started);
        return result;
    }

    // For the few queries that return a plain value, not a Result.
    public async Task<CallToolResult> RunPlainAsync<T>(string tool, Func<ClaimsPrincipal, Task<T>> call)
        where T : notnull
    {
        var caller = Caller();
        var started = time.GetTimestamp();
        var result = McpResults.Ok(await call(caller), Json);
        Record(tool, caller, result, started);
        return result;
    }

    // A call rejected before any handler ran (a malformed argument combination).
    public CallToolResult Reject(string tool, string message)
    {
        var caller = Caller();
        var result = McpResults.Invalid(message);
        Record(tool, caller, result, time.GetTimestamp());
        return result;
    }

    private ClaimsPrincipal Caller() =>
        http.HttpContext?.User ?? throw new InvalidOperationException("An MCP tool ran outside an HTTP request.");

    private void Record(string tool, ClaimsPrincipal caller, CallToolResult result, long started) =>
        logger.RecordToolCall(tool, caller.GetRequiredUserId().Value, McpResults.Outcome(result), (long)time.GetElapsedTime(started).TotalMilliseconds);
}
