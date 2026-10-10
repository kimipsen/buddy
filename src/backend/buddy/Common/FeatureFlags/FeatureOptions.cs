namespace buddy.Common.FeatureFlags;

// Which optional features this installation offers, bound from "Features:*" -- the family's
// operator turns one off with an environment variable (Features__Medicines=false). Every flag
// defaults to on, so an installation without the section behaves as before. Off hides a feature's
// endpoints and screens only: its registrations, data and GDPR coverage stay.
// See docs/backend/analysis/feature-flags.md.
public sealed class FeatureOptions
{
    public const string SectionName = "Features";

    public bool Mealplans { get; set; } = true;

    // Sub-flags of Mealplans: their endpoints live in the /mealplans group.
    public bool MealplanAiAssistant { get; set; } = true;
    public bool MealplanImport { get; set; } = true;

    public bool Medicines { get; set; } = true;
    public bool SleepDiary { get; set; } = true;
    public bool HouseRules { get; set; } = true;
    public bool Pickups { get; set; } = true;
    public bool Babysitters { get; set; } = true;
    public bool WorkLocations { get; set; } = true;
    public bool Printing { get; set; } = true;
    public bool Progress { get; set; } = true;
    public bool TaskLibrary { get; set; } = true;

    // Frontend only (the help panel and /guardian/help); carried here so the operator has one place.
    public bool Help { get; set; } = true;

    // A sub-flag left on under a disabled parent follows the parent rather than failing startup:
    // every flag defaults to on, so Mealplans=false alone is a complete instruction.
    public InstallationFeatures Effective() => new(
        Mealplans,
        MealplanAiAssistant && Mealplans,
        MealplanImport && Mealplans,
        Medicines,
        SleepDiary,
        HouseRules,
        Pickups,
        Babysitters,
        WorkLocations,
        Printing,
        Progress,
        TaskLibrary,
        Help);

    public IReadOnlyList<string> SubFlagsOverriddenByParent()
    {
        List<string> overridden = [];

        if (!Mealplans && MealplanAiAssistant)
        {
            overridden.Add(nameof(MealplanAiAssistant));
        }

        if (!Mealplans && MealplanImport)
        {
            overridden.Add(nameof(MealplanImport));
        }

        return overridden;
    }
}

// The effective flags: what Program.cs maps and what GET /features reports to the frontend.
public sealed record InstallationFeatures(
    bool Mealplans,
    bool MealplanAiAssistant,
    bool MealplanImport,
    bool Medicines,
    bool SleepDiary,
    bool HouseRules,
    bool Pickups,
    bool Babysitters,
    bool WorkLocations,
    bool Printing,
    bool Progress,
    bool TaskLibrary,
    bool Help);
