using System.Diagnostics;

namespace buddy.Features.Mcp;

// Log lines of the MCP server (docs/backend/analysis/mcp-server.md). Event ids 15000-15099.
// Never an argument value: tool name, caller, outcome and duration only. v1 has a log line and an
// activity tag, no metric.
internal static partial class McpLog
{
    public const string ToolTag = "mcp.tool";

    [LoggerMessage(EventId = 15001, Level = LogLevel.Information, Message = "MCP tool {Tool} called by user {UserId}: {Outcome} in {DurationMs} ms")]
    public static partial void ToolCalled(this ILogger logger, string tool, Guid userId, string outcome, long durationMs);

    [LoggerMessage(EventId = 15002, Level = LogLevel.Information, Message = "Rejected {Method} {Path}: the MCP caller is not in the buddy-guardian group")]
    public static partial void NonGuardianCallerRejected(this ILogger logger, string method, string? path);

    // The one place a tool call is recorded: the log line, and the tool name on the request's activity.
    public static void RecordToolCall(this ILogger logger, string tool, Guid userId, string outcome, long durationMs)
    {
        Activity.Current?.SetTag(ToolTag, tool);
        logger.ToolCalled(tool, userId, outcome, durationMs);
    }
}
