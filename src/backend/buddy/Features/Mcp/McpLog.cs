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

    // The summary of one cleanup pass; the operator's signal that someone is filling Keycloak's
    // registration endpoint is a spike in Deleted (or WouldDelete in a dry run).
    [LoggerMessage(EventId = 15003, Level = LogLevel.Information, Message = "MCP client cleanup (dryRun={DryRun}): candidates={Candidates}, stamped={Stamped}, deleted={Deleted}, wouldDelete={WouldDelete} [{WouldDeleteClientIds}], kept={Kept}, keptUnknown={KeptUnknown}, failed={Failed}")]
    public static partial void McpClientCleanupFinished(this ILogger logger, bool dryRun, int candidates, int stamped, int deleted, int wouldDelete, string wouldDeleteClientIds, int kept, int keptUnknown, int failed);

    [LoggerMessage(EventId = 15004, Level = LogLevel.Error, Message = "MCP client cleanup pass failed; nothing was deleted and the next pass tries again")]
    public static partial void McpClientCleanupFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 15005, Level = LogLevel.Error, Message = "MCP client cleanup reached its cap of {Cap} per pass; {Remaining} more expired clients are left for the next pass")]
    public static partial void McpClientCleanupCapReached(this ILogger logger, int cap, int remaining);

    [LoggerMessage(EventId = 15006, Level = LogLevel.Error, Message = "MCP client cleanup could not read every user's consents; every registered client is kept this pass")]
    public static partial void McpClientCleanupFailedClosed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 15007, Level = LogLevel.Information, Message = "MCP client cleanup is off (Mcp:ClientCleanup:Enabled=false); abandoned registered clients are not removed")]
    public static partial void McpClientCleanupDisabled(this ILogger logger);

    [LoggerMessage(EventId = 15008, Level = LogLevel.Warning, Message = "MCP client cleanup could not {Operation} client {ClientId}; the pass goes on")]
    public static partial void McpClientCleanupClientFailed(this ILogger logger, string operation, string clientId, Exception exception);

    // The one place a tool call is recorded: the log line, and the tool name on the request's activity.
    public static void RecordToolCall(this ILogger logger, string tool, Guid userId, string outcome, long durationMs)
    {
        Activity.Current?.SetTag(ToolTag, tool);
        logger.ToolCalled(tool, userId, outcome, durationMs);
    }
}
