using System.ComponentModel.DataAnnotations;

namespace buddy.Features.Mealplans;

// Bound from "Mealie:*" -- see docs/backend/analysis/mealie-integration.md, Question 3. The
// defaults are the production ones: https only, public addresses only.
public sealed class MealieOptions
{
    public const string SectionName = "Mealie";

    // A self-hosted Buddy on the same network as Mealie (and the dev stack) turns this on.
    public bool AllowPrivateNetworks { get; set; }

    // Development and self-hosted only.
    public bool AllowHttp { get; set; }

    // Empty: any host that passes the address check. Otherwise only these host names.
    public string[] AllowedHosts { get; set; } = [];

    [Range(1, 120)]
    public int TimeoutSeconds { get; set; } = 10;

    [Range(1024, 50 * 1024 * 1024)]
    public int MaxResponseBytes { get; set; } = 2 * 1024 * 1024;
}
