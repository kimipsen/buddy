using buddy.Common.FeatureFlags;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Xunit;

namespace buddy.IntegrationTests.Common.FeatureFlags;

// The parts of the feature flags that need no host (docs/backend/analysis/feature-flags.md);
// FeatureFlagsTests starts real hosts for the rest.
public sealed class FeatureOptionsTests
{
    [Fact]
    public void No_features_section_turns_every_feature_on()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection([]).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddFeatureFlagsFeature();
        using var provider = services.BuildServiceProvider();

        Assert.Equal(
            new InstallationFeatures(true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true),
            provider.GetRequiredService<IOptions<FeatureOptions>>().Value.Effective());
    }

    [Fact]
    public void Only_sub_flags_left_on_under_a_disabled_parent_are_reported_as_overridden()
    {
        Assert.Equal(["MealplanAiAssistant", "MealplanImport", "MealplanMealie"], new FeatureOptions { Mealplans = false }.SubFlagsOverriddenByParent());
        Assert.Equal(["MealplanImport", "MealplanMealie"], new FeatureOptions { Mealplans = false, MealplanAiAssistant = false }.SubFlagsOverriddenByParent());
        Assert.Equal(["MealplanAiAssistant", "MealplanImport"], new FeatureOptions { Mealplans = false, MealplanMealie = false }.SubFlagsOverriddenByParent());
        Assert.Empty(new FeatureOptions { MealplanAiAssistant = false }.SubFlagsOverriddenByParent());
        Assert.Equal(["VisitSummaries"], new FeatureOptions { SleepDiary = false, Medicines = false, Feelings = false }.SubFlagsOverriddenByParent());
        Assert.Empty(new FeatureOptions { SleepDiary = false, Medicines = false }.SubFlagsOverriddenByParent());
    }

    [Fact]
    public void Visit_summaries_stay_on_while_any_feature_they_report_on_is_on()
    {
        Assert.True(new FeatureOptions { SleepDiary = false, Medicines = false }.Effective().VisitSummaries);
        Assert.False(new FeatureOptions { SleepDiary = false, Medicines = false, Feelings = false }.Effective().VisitSummaries);
        Assert.False(new FeatureOptions { VisitSummaries = false }.Effective().VisitSummaries);
    }
}
