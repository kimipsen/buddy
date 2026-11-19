using System.ComponentModel.DataAnnotations;

namespace buddy.Features.Mcp;

// Mcp:ClientCleanup. The defaults are the failsafe -- deletion can't be undone and a bug could disconnect
// every family's AI apps -- so McpClientCleanupOptionsTests pins them as literals. A dry run first
// (DryRun on) lets the operator read one summary line before the job deletes anything.
public sealed class McpClientCleanupOptions
{
    public const string SectionName = "Mcp:ClientCleanup";

    // Off: McpClientCleanupService isn't registered (McpStartupLogger says so once at startup).
    public bool Enabled { get; set; } = true;

    // On: a pass stamps buddy.firstSeen and logs what it would delete, but never deletes.
    public bool DryRun { get; set; } = true;

    // Deletions (or, in a dry run, would-be deletions) per pass. 0 deletes nothing.
    [Range(0, 10_000)]
    public int MaxDeletesPerPass { get; set; } = 100;

    // Read at registration time, before the container exists: AddMcpFeature decides on this.
    public static bool IsEnabled(IConfiguration configuration) =>
        configuration.GetSection(SectionName).GetValue(nameof(Enabled), true);
}
