using System.ComponentModel;

using buddy.Features.Calendars;
using buddy.Features.Groups;
using buddy.Features.Guardians;

using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

using Wolverine;

namespace buddy.Features.Mcp;

[McpServerToolType]
public sealed class FamilyTools(IMessageBus bus, McpToolRunner runner)
{
    [McpServerTool(Name = ToolNames.ListChildren, ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false),
     Description("The children the guardian is linked to, with their ids. Other tools take a childId from here.")]
    public Task<CallToolResult> ListChildrenAsync(CancellationToken cancellationToken) =>
        runner.RunPlainAsync(
            ToolNames.ListChildren,
            async principal => new
            {
                items = (await bus.InvokeAsync<IReadOnlyCollection<ChildSummary>>(ListMyChildren.FromClaims(principal), cancellationToken))
                    .Select(child => new { id = child.Id, name = child.Name, kind = child.Kind, language = child.Language, timeZone = child.TimeZoneId })
                    .ToList()
            });

    [McpServerTool(Name = ToolNames.ListGroups, ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false),
     Description("The family groups (several households sharing meal plans and calendars) the guardian belongs to, with their ids. Tools that accept a groupId take it from here.")]
    public Task<CallToolResult> ListGroupsAsync(CancellationToken cancellationToken) =>
        runner.RunPlainAsync(
            ToolNames.ListGroups,
            async principal => new
            {
                items = (await bus.InvokeAsync<IReadOnlyCollection<GroupMembershipDocument>>(ListGroups.FromClaims(principal), cancellationToken))
                    .Select(m => new GroupSummaryResponse(new GroupId(m.GroupId), m.GroupName, m.Role))
                    .ToList()
            });
}
