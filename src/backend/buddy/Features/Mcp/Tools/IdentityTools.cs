using System.ComponentModel;

using buddy.Common;
using buddy.Features.Users;

using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

using Wolverine;

namespace buddy.Features.Mcp;

[McpServerToolType]
public sealed class IdentityTools(IMessageBus bus, McpToolRunner runner, TimeProvider time)
{
    [McpServerTool(Name = ToolNames.Whoami, ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false),
     Description("The signed-in guardian: their id, name, language and time zone. Call this first to learn who is asking and in which language to answer.")]
    public Task<CallToolResult> WhoamiAsync(CancellationToken cancellationToken) =>
        runner.RunAsync<User>(
            ToolNames.Whoami,
            principal => bus.InvokeAsync<Result<User>>(GetMe.FromClaims(principal), cancellationToken),
            user => new { id = user.Id, name = user.Name, language = user.Language, timeZone = user.TimeZoneId });

    [McpServerTool(Name = ToolNames.GetToday, ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false),
     Description("The guardian's current local date, time and time zone. Use it to turn \"today\", \"tomorrow\" or \"this week\" into the dates other tools need.")]
    public Task<CallToolResult> GetTodayAsync(CancellationToken cancellationToken) =>
        runner.RunAsync<User>(
            ToolNames.GetToday,
            principal => bus.InvokeAsync<Result<User>>(GetMe.FromClaims(principal), cancellationToken),
            user =>
            {
                var local = TimeZoneInfo.ConvertTime(time.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(user.TimeZoneId.Value));
                return new
                {
                    date = DateOnly.FromDateTime(local.DateTime),
                    time = new TimeOnly(local.Hour, local.Minute),
                    dayOfWeek = local.DayOfWeek.ToString(),
                    timeZone = user.TimeZoneId,
                };
            });
}
