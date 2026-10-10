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
            new InstallationFeatures(true, true, true, true, true, true, true, true, true, true, true, true, true),
            provider.GetRequiredService<IOptions<FeatureOptions>>().Value.Effective());
    }

    [Fact]
    public void Only_sub_flags_left_on_under_a_disabled_parent_are_reported_as_overridden()
    {
        Assert.Equal(["MealplanAiAssistant", "MealplanImport"], new FeatureOptions { Mealplans = false }.SubFlagsOverriddenByParent());
        Assert.Equal(["MealplanImport"], new FeatureOptions { Mealplans = false, MealplanAiAssistant = false }.SubFlagsOverriddenByParent());
        Assert.Empty(new FeatureOptions { MealplanAiAssistant = false }.SubFlagsOverriddenByParent());
    }
}
