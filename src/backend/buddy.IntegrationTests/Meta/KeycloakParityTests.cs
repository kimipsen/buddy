using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using buddy.IntegrationTests.Fixtures;

using Xunit;

namespace buddy.IntegrationTests.Meta;

// The integration tests must run the Keycloak the app runs on, configured like the app's realm.
// They once stayed on 21.1.1 while the devcontainer and the Azure image moved to 26.x, so 26.x-only
// behaviour (the "basic" scope carrying `sub`, the declarative user profile's VERIFY_PROFILE)
// passed here and broke e2e on a fresh realm. No containers needed: everything is read from the
// checked-in files.
public sealed partial class KeycloakParityTests
{
    private const string UserProfileProvider = "org.keycloak.userprofile.UserProfileProvider";

    [Theory]
    [InlineData(".devcontainer/docker-compose.yml")]
    [InlineData("deploy/azure/keycloak/Dockerfile")]
    public void The_fixture_runs_the_same_Keycloak_image_as(string path)
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot(), path));

        var images = KeycloakImageReference().Matches(text).Select(match => match.Groups["image"].Value).Distinct().ToArray();

        Assert.True(images.Length > 0, $"No quay.io/keycloak/keycloak image reference in {path}.");
        Assert.All(images, image => Assert.Equal(BuddyApiFixture.KeycloakImage, image));
    }

    // Which attributes are required decides whether Keycloak lets a user sign in (a child has no
    // email), so the test realm must enforce exactly what the app realm does.
    [Fact]
    public void The_test_realm_has_the_same_user_profile_as_the_app_realm()
    {
        var appProfile = UserProfile(".devcontainer/keycloak/buddy-realm.json");
        var testProfile = UserProfile("src/backend/buddy.IntegrationTests/Fixtures/TestRealm.json");

        Assert.True(
            JsonNode.DeepEquals(appProfile, testProfile),
            $"TestRealm.json's user profile differs from buddy-realm.json's. Copy the app realm's {UserProfileProvider} component.\n" +
            $"App:  {appProfile?.ToJsonString()}\nTest: {testProfile?.ToJsonString()}");
    }

    // The profile is a JSON document stored as a string in the provider component's config.
    private static JsonNode? UserProfile(string realmPath)
    {
        var realm = JsonNode.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), realmPath)));
        var config = realm?["components"]?[UserProfileProvider]?[0]?["config"]?["kc.user.profile.config"]?[0]?.GetValue<string>()
            ?? throw new InvalidOperationException($"{realmPath} has no {UserProfileProvider} component with a kc.user.profile.config.");

        return JsonNode.Parse(config);
    }

    // The tag, without a trailing "@sha256:..." digest pin.
    [GeneratedRegex(@"(?<image>quay\.io/keycloak/keycloak:[^\s@""']+)")]
    private static partial Regex KeycloakImageReference();

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "docs", "backend")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException($"No repository root (a folder with docs/backend) above {AppContext.BaseDirectory}.");
    }
}
