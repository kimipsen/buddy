using System.Diagnostics;

using buddy.Features.Mcp;
using buddy.IntegrationTests.Common;

using Microsoft.Extensions.Logging;

using Xunit;

namespace buddy.IntegrationTests.Features.Mcp;

// McpLog.ToolCalled: event id in the 15000 block, structured fields only, and the mcp.tool activity tag.
public sealed class McpLogTests
{
    [Fact]
    public void A_tool_call_logs_tool_user_outcome_and_duration_and_tags_the_activity()
    {
        var logs = new CapturingLoggerProvider();
        using var factory = LoggerFactory.Create(builder => builder.AddProvider(logs));
        var logger = factory.CreateLogger("test");
        var userId = Guid.NewGuid();

        using var activity = new Activity("mcp-test").Start();
        logger.RecordToolCall("list_tasks", userId, "ok", 12);

        var entry = Assert.Single(logs.Entries);
        Assert.Equal(15001, entry.EventId);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("list_tasks", entry.Property("Tool"));
        Assert.Equal(userId, entry.Property("UserId"));
        Assert.Equal("ok", entry.Property("Outcome"));
        Assert.Equal(12L, entry.Property("DurationMs"));
        Assert.Equal("list_tasks", activity.GetTagItem(McpLog.ToolTag));
    }
}
